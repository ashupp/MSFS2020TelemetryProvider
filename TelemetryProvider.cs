using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Threading;
using Microsoft.FlightSimulator.SimConnect;
using SimFeedback.log;

namespace SimFeedback.telemetry
{
    public sealed class TelemetryProvider : AbstractTelemetryProvider
    {
        private bool _isStopped = true;
        const int WM_USER_SIMCONNECT = 0x0402;
        private bool SimConnectInitialized = false;
        //private SimConnect simconnect = null;
        private CancellationTokenSource cts = null;
        private Thread _t;

        private Object simconnectx;
        //private SimConnect _simconnect;
        private bool _definitionsAdded;
        private TelemetryData lastTelemetryData;
        private Process _currProcess;
        private IntPtr _mainWindowHandle;



        public TelemetryProvider()
        {
            Author = "ashupp / ashnet GmbH";
            Version = Assembly.LoadFrom(Assembly.GetExecutingAssembly().Location).GetName().Version.ToString();
            BannerImage = @"img\banner_" + Name + ".png";
            IconImage = @"img\icon_" + Name + ".png";
            TelemetryUpdateFrequency = 30;
        }

        public override string Name => "msfs2020";

        public override void Init(ILogger logger)
        {
            base.Init(logger);
            Log("Initializing " + Name + "TelemetryProvider");
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

        private void InitializeSimConnect()
        {
            LogDebug(Name + " InitializeSimConnect");
            SimConnectInitialized = true;

            _currProcess = Process.GetCurrentProcess();
            var mainWindowHandle = _currProcess.MainWindowHandle;

            try
            {
                if (RuntimePolicyHelper.LegacyV2RuntimeEnabledSuccessfully)
                {
                    LogDebug("Init SimConnect");
                    _currProcess = Process.GetCurrentProcess();
                    _mainWindowHandle = _currProcess.Handle;
                    var simconnect = new SimConnect("SimFeedbackSimconnect", _mainWindowHandle, WM_USER_SIMCONNECT, null, 0);

                    simconnectx = simconnect;
                    LogDebug("Got Simconnect and Handle " + _mainWindowHandle);
                    // listen to connect and quit msgs
                    simconnect.OnRecvOpen += new SimConnect.RecvOpenEventHandler(Simconnect_OnRecvOpen);
                    simconnect.OnRecvQuit += new SimConnect.RecvQuitEventHandler(Simconnect_OnRecvQuit);

                    // listen to exceptions
                    simconnect.OnRecvException += Simconnect_OnRecvException;

                    simconnect.OnRecvSimobjectData += Simconnect_OnRecvSimobjectData;
                    RegisterAircraftDataDefinition();
                    RegisterFlightStatusDefinition();

                    simconnect.SubscribeToSystemEvent(EVENTS.POSITION_CHANGED, "PositionChanged");
                    simconnect.OnRecvEvent += Simconnect_OnRecvEvent;
                    LogDebug(Name + " Initialized");
                }
                else
                {
                    LogError("Could not change runtimePolicy...");
                }
            }
            catch (Exception e)
            {
                LogError("Could not change runtimePolicy..." + e.Message);
            }
        }

        private void Run()
        {
            try
            {
                LogDebug(Name + "TelemetryProvider Run start");
                lastTelemetryData = new TelemetryData();
                Stopwatch sw = new Stopwatch();
                sw.Start();
                LogDebug(Name + "TelemetryProvider Stopwatch start");
                
                while (!_isStopped)
                {

                    
                    try
                    {

                        if (!SimConnectInitialized)
                        {
                            InitializeSimConnect();
                        }
                        else
                        {
                            var simconnect = simconnectx as SimConnect;
                            simconnect?.ReceiveMessage();
                            sw.Restart();
                            Thread.Sleep(TelemetryUpdateFrequency);
                        }

                        if (sw.ElapsedMilliseconds > 500)
                        {
                            IsRunning = false;
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
                _definitionsAdded = false;
                SimConnectInitialized = false;
                IsConnected = false;
                IsRunning = false;
            }
            catch (Exception e)
            {
                LogDebug(Name + " Error: " + e.Message);
            }
        }

        #region Private Methods

        #region Register Data Definitions

        private void RegisterAircraftDataDefinition()
        {
            var simconnect = simconnectx as SimConnect;
            simconnect.AddToDataDefinition(DEFINITIONS.AircraftData,
                "ATC TYPE",
                null,
                SIMCONNECT_DATATYPE.STRING32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.AircraftData,
                "ATC MODEL",
                null,
                SIMCONNECT_DATATYPE.STRING32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.AircraftData,
                "Title",
                null,
                SIMCONNECT_DATATYPE.STRING256,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.AircraftData,
                "ESTIMATED CRUISE SPEED",
                "Knots",
                SIMCONNECT_DATATYPE.FLOAT32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            // IMPORTANT: register it with the simconnect managed wrapper marshaller
            // if you skip this step, you will only receive a uint in the .dwData field.
            simconnect.RegisterDataDefineStruct<AircraftDataStruct>(DEFINITIONS.AircraftData);
        }

        private void RegisterFlightStatusDefinition()
        {
            var simconnect = simconnectx as SimConnect;
            //simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
            //    "SIM TIME",
            //    "Seconds",
            //    SIMCONNECT_DATATYPE.FLOAT32,
            //    0.0f,
            //    SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "SIMULATION RATE",
                "number",
                SIMCONNECT_DATATYPE.INT32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "PLANE LATITUDE",
                "Degrees",
                SIMCONNECT_DATATYPE.FLOAT32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "PLANE LONGITUDE",
                "Degrees",
                SIMCONNECT_DATATYPE.FLOAT32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "PLANE ALTITUDE",
                "Feet",
                SIMCONNECT_DATATYPE.FLOAT32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "PLANE ALT ABOVE GROUND",
                "Feet",
                SIMCONNECT_DATATYPE.FLOAT32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "ACCELERATION BODY X",
                "Feet per second squared",
                SIMCONNECT_DATATYPE.FLOAT32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "ACCELERATION BODY Y",
                "Feet per second squared",
                SIMCONNECT_DATATYPE.FLOAT32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "ACCELERATION BODY Z",
                "Feet per second squared",
                SIMCONNECT_DATATYPE.FLOAT32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "ROTATION VELOCITY BODY X",
                "Feet per second",
                SIMCONNECT_DATATYPE.FLOAT32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "ROTATION VELOCITY BODY Y",
                "Feet per second",
                SIMCONNECT_DATATYPE.FLOAT32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "ROTATION VELOCITY BODY Z",
                "Feet per second",
                SIMCONNECT_DATATYPE.FLOAT32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "PLANE PITCH DEGREES",
                "Radians",
                SIMCONNECT_DATATYPE.FLOAT32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "PLANE BANK DEGREES",
                "Radians",
                SIMCONNECT_DATATYPE.FLOAT32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "PLANE HEADING DEGREES TRUE",
                "Degrees",
                SIMCONNECT_DATATYPE.FLOAT32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "PLANE HEADING DEGREES MAGNETIC",
                "Degrees",
                SIMCONNECT_DATATYPE.FLOAT32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "GROUND ALTITUDE",
                "Meters",
                SIMCONNECT_DATATYPE.FLOAT32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "GROUND VELOCITY",
                "Knots",
                SIMCONNECT_DATATYPE.FLOAT32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "AIRSPEED INDICATED",
                "Knots",
                SIMCONNECT_DATATYPE.FLOAT32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "VERTICAL SPEED",
                "Feet per minute",
                SIMCONNECT_DATATYPE.FLOAT32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "FUEL TOTAL QUANTITY",
                "Gallons",
                SIMCONNECT_DATATYPE.FLOAT32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "AMBIENT WIND VELOCITY",
                "Feet per second",
                SIMCONNECT_DATATYPE.FLOAT32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "AMBIENT WIND DIRECTION",
                "Degrees",
                SIMCONNECT_DATATYPE.FLOAT32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "GENERAL ENG RPM:1",
                "Degrees",
                SIMCONNECT_DATATYPE.FLOAT32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "SIM ON GROUND",
                "number",
                SIMCONNECT_DATATYPE.INT32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "STALL WARNING",
                "number",
                SIMCONNECT_DATATYPE.INT32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "OVERSPEED WARNING",
                "number",
                SIMCONNECT_DATATYPE.INT32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "AUTOPILOT MASTER",
                "number",
                SIMCONNECT_DATATYPE.INT32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "TRANSPONDER CODE:1",
                "Hz",
                SIMCONNECT_DATATYPE.INT32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "COM ACTIVE FREQUENCY:1",
                "kHz",
                SIMCONNECT_DATATYPE.INT32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                "COM ACTIVE FREQUENCY:2",
                "kHz",
                SIMCONNECT_DATATYPE.INT32,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);

            // IMPORTANT: register it with the simconnect managed wrapper marshaller
            // if you skip this step, you will only receive a uint in the .dwData field.
            simconnect.RegisterDataDefineStruct<FlightStatusStruct>(DEFINITIONS.FlightStatus);
        }

        #endregion

        private void Simconnect_OnRecvSimobjectData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
        {
            // Must be general SimObject information
            switch (data.dwRequestID)
            {
                case (uint)DATA_REQUESTS.FLIGHT_STATUS:
                    {
                        var flightStatus = data.dwData[0] as FlightStatusStruct?;

                        if (flightStatus.HasValue)
                        {
                            try
                            {
                                //object obj = data.dwData[0];
                                //AircraftData acData = (AircraftData?)obj ?? default;
                                TelemetryData telemetryData = new TelemetryData
                                {
                                    Pitch = (float)flightStatus.Value.Pitch,
                                    Roll = (float)flightStatus.Value.Bank,
                                    Yaw = (float)flightStatus.Value.Yaw,
                                    Surge = (float)flightStatus.Value.zAccel,
                                    Sway = (float)flightStatus.Value.xAccel,
                                    Heave = (float)flightStatus.Value.yAccel,
                                    RollSpeed = (float)flightStatus.Value.zVelocity,
                                    YawSpeed = (float)flightStatus.Value.yVelocity,
                                    PitchSpeed = (float)flightStatus.Value.xVelocity,
                                    Speed = (float)flightStatus.Value.GroundSpeed,
                                    RPM = (float)flightStatus.Value.RPM
                                };

                                IsConnected = true;
                                IsRunning = true;

                                TelemetryEventArgs args = new TelemetryEventArgs(new TelemetryInfoElem(telemetryData, lastTelemetryData));
                                RaiseEvent(OnTelemetryUpdate, args);
                                lastTelemetryData = telemetryData;
                            }
                            catch (Exception e)
                            {
                                LogError(Name + "TelemetryProvider Exception while receiving data", e);
                                IsConnected = false;
                                IsRunning = false;
                                Thread.Sleep(1000);
                            }
                        }
                    }
                    break;
            }
        }

        void Simconnect_OnRecvEvent(SimConnect sender, SIMCONNECT_RECV_EVENT data)
        {
            LogDebug("OnRecvEvent dwID " + data.dwID + " uEventID " + data.uEventID);
            switch ((SIMCONNECT_RECV_ID)data.dwID)
            {
                case SIMCONNECT_RECV_ID.EVENT_FILENAME:

                    break;
                case SIMCONNECT_RECV_ID.QUIT:
                    LogDebug("Quit");
                    break;
            }

            switch ((EVENTS)data.uEventID)
            {
                case EVENTS.POSITION_CHANGED:
                    LogDebug("Position changed");
                    break;
            }
        }


        void Simconnect_OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
        {
            LogDebug("Connected to Flight Simulator");
            var simconnect = simconnectx as SimConnect;
            simconnect.RequestDataOnSimObject(DATA_REQUESTS.FLIGHT_STATUS, DEFINITIONS.FlightStatus, 0, SIMCONNECT_PERIOD.SIM_FRAME, SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT, 0, 0, 0);
        }

        // The case where the user closes Flight Simulator
        void Simconnect_OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
        {
            LogDebug("Flight Simulator has exited");
            CloseConnection();
        }

        void Simconnect_OnRecvException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
        {
            LogError("Exception received: {error} " +  (SIMCONNECT_EXCEPTION)data.dwException);
        }

        private void RecoverFromError(Exception exception)
        {
            // 0xC000014B: CTD
            // 0xC00000B0: Sim has exited
            LogDebug("Exception received " + exception.Message);
            CloseConnection();
        }

        public void CloseConnection()
        {
            try
            {
                cts?.Cancel();
                cts = null;
            }
            catch (Exception ex)
            {
                LogError($"Cannot cancel request loop! Error: {ex.Message}");
            }
            try
            {
                var simconnect = simconnectx as SimConnect;
                if (simconnect != null)
                {
                    // Dispose serves the same purpose as SimConnect_Close()
                    simconnect.Dispose();
                    simconnect = null;
                }
            }
            catch (Exception ex)
            {
                LogError($"Cannot unsubscribe events! Error: {ex.Message}");
            }
            SimConnectInitialized = false;
        }

        #endregion

    }
}