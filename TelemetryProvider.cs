using System;
using System.Configuration;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using Microsoft.FlightSimulator.SimConnect;
using SimFeedback.log;

namespace SimFeedback.telemetry
{
    public sealed class TelemetryProvider : AbstractTelemetryProvider
    {
        #region Members

        private bool _isStopped = true;
        private bool _connectionClosed;

        private int _telemetryUpdateFrequency;
        private int _fixedRateLimiter;
        private bool _autoCalculateRateLimiter;
        private bool _disableRateThrottle;

        private const int WM_USER_SIMCONNECT = 0x0402;
        private bool _simConnectInitialized;
        private CancellationTokenSource _cts;
        private Thread _t;
        private SimConnect _simconnect;
        private TelemetryData _lastTelemetryData;
        private IntPtr _mainWindowHandle;

        #endregion

        #region Public methods

        public TelemetryProvider()
        {
            Author = "ashupp / ashnet GmbH";
            Version = Assembly.LoadFrom(Assembly.GetExecutingAssembly().Location).GetName().Version.ToString();
            BannerImage = @"img\banner_" + Name + ".png";
            IconImage = @"img\icon_" + Name + ".png";
        }

        public override string Name => "msfs2020";

        public override void Init(ILogger logger)
        {
            base.Init(logger);
            Log("Initializing " + Name + "TelemetryProvider " + Version);

            ExeConfigurationFileMap map = new ExeConfigurationFileMap();
            map.ExeConfigFilename = Assembly.GetExecutingAssembly().Location + ".config";

            Configuration libConfig = ConfigurationManager.OpenMappedExeConfiguration(map, ConfigurationUserLevel.None);
            AppSettingsSection section = (libConfig.GetSection("appSettings") as AppSettingsSection);
            if (section != null)
            {
                _telemetryUpdateFrequency = Convert.ToInt32(section.Settings["TelemetryUpdateFrequency"].Value);
                TelemetryUpdateFrequency = _telemetryUpdateFrequency;
                _fixedRateLimiter = 1000 / _telemetryUpdateFrequency;
                _autoCalculateRateLimiter = Convert.ToBoolean(section.Settings["AutoCalculateRateLimiter"].Value);
                _disableRateThrottle = Convert.ToBoolean(section.Settings["DisableRateThrottle"].Value);
            }

            LogDebug("TelemetryUpdateFrequency: " + TelemetryUpdateFrequency);
            LogDebug("SamplePeriod: " + SamplePeriod);
            LogDebug("AutoCalculateRateLimiter: " + _autoCalculateRateLimiter);
            LogDebug("FixedRateLimiter: " + _fixedRateLimiter);
            LogDebug("DisableRateThrottle: " + _disableRateThrottle);
        }

        public override string[] GetValueList()
        {
            return GetValueListByReflection(typeof(TelemetryData));
        }

        public override void Stop()
        {
            if (_isStopped) return;
            LogDebug("Stopping " + Name + "TelemetryProvider");
            _isStopped = true;
            if (_t != null) _t.Join();
        }

        public override void Start()
        {
            if (_isStopped)
            {
                LogDebug("Starting " + Name + "TelemetryProvider");
                _isStopped = false;
                _t = new Thread(Run);
                _t.Start();
            }
        }

        #endregion

        #region Private methods 

        private void InitializeSimConnect()
        {
            LogDebug(Name + " InitializeSimConnect");
            _simConnectInitialized = true;

            try
            {
                if (RuntimePolicyHelper.LegacyV2RuntimeEnabledSuccessfully)
                {
                    try
                    {
                        LogDebug("Init SimConnect");
                        var currProcess = Process.GetCurrentProcess();
                        _mainWindowHandle = currProcess.Handle;
                        _simconnect = new SimConnect("SimFeedbackSimconnect", _mainWindowHandle, WM_USER_SIMCONNECT, null, 0);

                        LogDebug("Got Simconnect and Handle " + _mainWindowHandle);
                        // listen to connect and quit msgs
                        _simconnect.OnRecvOpen += Simconnect_OnRecvOpen;
                        _simconnect.OnRecvQuit += Simconnect_OnRecvQuit;

                        // listen to exceptions
                        _simconnect.OnRecvException += Simconnect_OnRecvException;
                        _simconnect.OnRecvSimobjectData += Simconnect_OnRecvSimobjectData;
                        RegisterFlightStatusDefinition();
                        LogDebug(Name + " Initialized");
                    }
                    catch (Exception e)
                    {
                        LogDebug("Simconnect not ready..." + e.Message);
                        _simConnectInitialized = false;
                    }

                }
                else
                {
                    LogError("Could not change runtimePolicy...");
                }
            }
            catch (Exception e)
            {
                LogError("Could not change runtimePolicy - exception: " + e.Message);
                _simConnectInitialized = false;
            }
        }

        private void Run()
        {
            try
            {
                LogDebug(Name + "TelemetryProvider Run start");
                _lastTelemetryData = new TelemetryData();
                Stopwatch sw = new Stopwatch();
                sw.Start();
                LogDebug(Name + "TelemetryProvider Stopwatch start");
                while (!_isStopped)
                {
                    try
                    {

                        if (!_simConnectInitialized)
                        {
                            InitializeSimConnect();
                        }
                        else
                        {
                            _simconnect?.ReceiveMessage();

                            if(!_disableRateThrottle){
                                if (_autoCalculateRateLimiter)
                                {
                                    var sleepMs = (int)(1000 / TelemetryUpdateFrequency - sw.ElapsedMilliseconds);
                                    if (sleepMs > 0) Thread.Sleep(sleepMs);
                                }
                                else
                                {
                                    Thread.Sleep(_fixedRateLimiter);
                                }

                                if (sw.ElapsedMilliseconds > 500)
                                {
                                    IsRunning = false;
                                }
                            }
                            sw.Restart();
                        }
                    }
                    catch (Exception e)
                    {
                        LogError(Name + "TelemetryProvider Exception while processing data", e);
                        IsConnected = false;
                        IsRunning = false;
                        Thread.Sleep(1000);
                    }
                }

                if (!_connectionClosed)
                {
                    CloseConnection();
                }
                _simConnectInitialized = false;
                IsConnected = false;
                IsRunning = false;
                _connectionClosed = false;
            }
            catch (Exception e)
            {
                LogDebug(Name + " Error: " + e.Message);
            }
        }

        private void CloseConnection()
        {
            _connectionClosed = true;
            LogDebug("CloseConnection");
            try
            {
                _cts?.Cancel();
                _cts = null;
            }
            catch (Exception ex)
            {
                LogError($"Cannot cancel request loop! Error: {ex.Message}");
            }
            try
            {
                if (_simconnect != null)
                {
                    _simconnect.OnRecvOpen -= Simconnect_OnRecvOpen;
                    _simconnect.OnRecvQuit -= Simconnect_OnRecvQuit;
                    _simconnect.OnRecvException -= Simconnect_OnRecvException;
                    _simconnect.OnRecvSimobjectData -= Simconnect_OnRecvSimobjectData;
                    // Dispose serves the same purpose as SimConnect_Close()
                    _simconnect.Dispose();
                    _isStopped = true;
                }
            }
            catch (Exception ex)
            {
                LogError("Cannot unsubscribe events! Error: " + ex.Message);
            }
            _simConnectInitialized = false;
        }

        #endregion

        #region Simconnect register data definitions

        private void RegisterFlightStatusDefinition()
        {
            if (_simconnect != null)
            {
                _simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                    "ACCELERATION BODY X",
                    "Feet per second squared",
                    SIMCONNECT_DATATYPE.FLOAT32,
                    0.0f,
                    SimConnect.SIMCONNECT_UNUSED);

                _simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                    "ACCELERATION BODY Y",
                    "Feet per second squared",
                    SIMCONNECT_DATATYPE.FLOAT32,
                    0.0f,
                    SimConnect.SIMCONNECT_UNUSED);

                _simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                    "ACCELERATION BODY Z",
                    "Feet per second squared",
                    SIMCONNECT_DATATYPE.FLOAT32,
                    0.0f,
                    SimConnect.SIMCONNECT_UNUSED);

                _simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                    "ROTATION VELOCITY BODY X",
                    "Feet per second",
                    SIMCONNECT_DATATYPE.FLOAT32,
                    0.0f,
                    SimConnect.SIMCONNECT_UNUSED);

                _simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                    "ROTATION VELOCITY BODY Y",
                    "Feet per second",
                    SIMCONNECT_DATATYPE.FLOAT32,
                    0.0f,
                    SimConnect.SIMCONNECT_UNUSED);

                _simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                    "ROTATION VELOCITY BODY Z",
                    "Feet per second",
                    SIMCONNECT_DATATYPE.FLOAT32,
                    0.0f,
                    SimConnect.SIMCONNECT_UNUSED);

                _simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                    "PLANE PITCH DEGREES",
                    "Radians",
                    SIMCONNECT_DATATYPE.FLOAT32,
                    0.0f,
                    SimConnect.SIMCONNECT_UNUSED);

                _simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                    "PLANE BANK DEGREES",
                    "Radians",
                    SIMCONNECT_DATATYPE.FLOAT32,
                    0.0f,
                    SimConnect.SIMCONNECT_UNUSED);

                _simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                    "PLANE HEADING DEGREES TRUE",
                    "Degrees",
                    SIMCONNECT_DATATYPE.FLOAT32,
                    0.0f,
                    SimConnect.SIMCONNECT_UNUSED);

                _simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                    "PLANE HEADING DEGREES MAGNETIC",
                    "Degrees",
                    SIMCONNECT_DATATYPE.FLOAT32,
                    0.0f,
                    SimConnect.SIMCONNECT_UNUSED);

                _simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                    "AIRSPEED TRUE",
                    "Knots",
                    SIMCONNECT_DATATYPE.FLOAT32,
                    0.0f,
                    SimConnect.SIMCONNECT_UNUSED);

                _simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                    "VERTICAL SPEED",
                    "Feet per minute",
                    SIMCONNECT_DATATYPE.FLOAT32,
                    0.0f,
                    SimConnect.SIMCONNECT_UNUSED);

                _simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                    "AMBIENT WIND VELOCITY",
                    "Feet per second",
                    SIMCONNECT_DATATYPE.FLOAT32,
                    0.0f,
                    SimConnect.SIMCONNECT_UNUSED);

                _simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                    "AMBIENT WIND DIRECTION",
                    "Degrees",
                    SIMCONNECT_DATATYPE.FLOAT32,
                    0.0f,
                    SimConnect.SIMCONNECT_UNUSED);

                _simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                    "GENERAL ENG RPM:1",
                    "Degrees",
                    SIMCONNECT_DATATYPE.FLOAT32,
                    0.0f,
                    SimConnect.SIMCONNECT_UNUSED);

                _simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                    "INCIDENCE ALPHA",
                    "Radians",
                    SIMCONNECT_DATATYPE.FLOAT32,
                    0.0f,
                    SimConnect.SIMCONNECT_UNUSED);

                _simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                    "INCIDENCE BETA",
                    "Radians",
                    SIMCONNECT_DATATYPE.FLOAT32,
                    0.0f,
                    SimConnect.SIMCONNECT_UNUSED);

                // IMPORTANT: register it with the simconnect managed wrapper marshaller
                // if you skip this step, you will only receive a uint in the .dwData field.
                _simconnect.RegisterDataDefineStruct<FlightStatusStruct>(DEFINITIONS.FlightStatus);
            }
            else
            {
                LogError("RegisterFlightStatusDefinition: Simconnect not ready");
            }
        }

        #endregion

        #region Simconnect eventhandlers 

        private void Simconnect_OnRecvSimobjectData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
        {
            try
            {
                // Must be general SimObject information
                if (data.dwRequestID == (uint) DATA_REQUESTS.FLIGHT_STATUS)
                {
                    var flightStatus = data.dwData[0] as FlightStatusStruct?;

                    if (!flightStatus.HasValue) return;
                    TelemetryData telemetryData = new TelemetryData
                    {
                        Pitch = flightStatus.Value.Pitch,
                        Roll = flightStatus.Value.Bank,
                        Yaw = flightStatus.Value.Yaw,
                        Surge = flightStatus.Value.zAccel,
                        Sway = flightStatus.Value.xAccel,
                        Heave = flightStatus.Value.yAccel,
                        RollSpeed = flightStatus.Value.zVelocity,
                        YawSpeed = flightStatus.Value.yVelocity,
                        PitchSpeed = flightStatus.Value.xVelocity,
                        RPM = flightStatus.Value.RPM,
                        AngleOfAttack = flightStatus.Value.AngleOfAttack,
                        AngleOfSideslip = flightStatus.Value.AngleOfSideslip,
                        AirSpeedTrue = flightStatus.Value.AirSpeedTrue
                    };

                    IsConnected = true;
                    IsRunning = true;

                    TelemetryEventArgs args = new TelemetryEventArgs(new TelemetryInfoElem(telemetryData, _lastTelemetryData)); RaiseEvent(OnTelemetryUpdate, args);
                    _lastTelemetryData = telemetryData;
                }
            }
            catch (Exception e)
            {
                LogError(Name + "TelemetryProvider Exception while receiving data:" + e.Message);
                IsConnected = false;
                IsRunning = false;
                Thread.Sleep(1000);
            }
        }
        
        private void Simconnect_OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
        {
            LogDebug("Connected to Flight Simulator");
            if (_simconnect != null)
            {
                _simconnect.RequestDataOnSimObject(DATA_REQUESTS.FLIGHT_STATUS, DEFINITIONS.FlightStatus, 0, SIMCONNECT_PERIOD.SIM_FRAME, SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT, 0, 0, 0);
            }
            else
            {
                LogError("SimConnect gone while opening");
            }
                

        }

        // The case where the user closes Flight Simulator
        private void Simconnect_OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
        {
            LogDebug("Flight Simulator has exited");
            CloseConnection();
        }

        private void Simconnect_OnRecvException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
        {
            LogError("Exception received: {error} " +  (SIMCONNECT_EXCEPTION)data.dwException);
        }

        #endregion
    }
}