using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.FlightSimulator.SimConnect;
using Newtonsoft.Json;
using SimFeedback.telemetry;


namespace MSFS2020DataProxy
{
    class SimConnectDataGrabber
    {
        private bool _isStopped = true;
        private bool _connectionClosed;

        private const int WM_USER_SIMCONNECT = 0x0402;
        public bool IsSimConnectInitialized;
        private CancellationTokenSource _cts;
        private Thread _t;
        private SimConnect _simconnect;
        private TelemetryData _lastTelemetryData;
        private IntPtr _mainWindowHandle;
        private UdpClient udpClient;



        public event EventHandler DataAvailable;

        protected virtual void OnThresholdReached(EventArgs e)
        {
            EventHandler handler = DataAvailable;
            handler?.Invoke(this, e);
        }

        public void InitializeSimConnect()
        {
            Console.WriteLine(" InitializeSimConnect");
            IsSimConnectInitialized = true;

            try
            {
                try
                {
                    Console.WriteLine("Init SimConnect");
                    var currProcess = Process.GetCurrentProcess();
                    _mainWindowHandle = currProcess.Handle;
                    _simconnect = new SimConnect("SimFeedbackSimconnect", _mainWindowHandle, WM_USER_SIMCONNECT, null, 0);

                    Console.WriteLine("Got Simconnect and Handle " + _mainWindowHandle);
                    // listen to connect and quit msgs
                    _simconnect.OnRecvOpen += Simconnect_OnRecvOpen;
                    _simconnect.OnRecvQuit += Simconnect_OnRecvQuit;

                    // listen to exceptions
                    _simconnect.OnRecvException += Simconnect_OnRecvException;
                    _simconnect.OnRecvSimobjectData += Simconnect_OnRecvSimobjectData;
                    RegisterFlightStatusDefinition();

                    // Start udp 
                    var ipEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 55280);
                    udpClient = new UdpClient();
                    udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    udpClient.Connect(ipEndPoint);



                    Console.WriteLine(" Initialized");
                }
                catch (Exception e)
                {
                    Console.WriteLine("Simconnect not ready..." + e.Message);
                    IsSimConnectInitialized = false;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Could not change runtimePolicy - exception: " + e.Message);
                IsSimConnectInitialized = false;
            }
        }

        private void RegisterFlightStatusDefinition()
        {
            if (_simconnect != null)
            {
                _simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                    "SIMULATION RATE",
                    "number",
                    SIMCONNECT_DATATYPE.INT32,
                    0.0f,
                    SimConnect.SIMCONNECT_UNUSED);

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
                    "GROUND ALTITUDE",
                    "Meters",
                    SIMCONNECT_DATATYPE.FLOAT32,
                    0.0f,
                    SimConnect.SIMCONNECT_UNUSED);

                _simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                    "GROUND VELOCITY",
                    "Knots",
                    SIMCONNECT_DATATYPE.FLOAT32,
                    0.0f,
                    SimConnect.SIMCONNECT_UNUSED);

                _simconnect.AddToDataDefinition(DEFINITIONS.FlightStatus,
                    "AIRSPEED INDICATED",
                    "Knots",
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
                Console.WriteLine("RegisterFlightStatusDefinition: Simconnect not ready");
            }
        }

        #region Simconnect eventhandlers 

        private void Simconnect_OnRecvSimobjectData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
        {
            try
            {
                // Must be general SimObject information
                if (data.dwRequestID == (uint)DATA_REQUESTS.FLIGHT_STATUS)
                {
                    var flightStatus = data.dwData[0] as FlightStatusStruct?;

                    if (!flightStatus.HasValue) return;

                    // Daten per UDP senden

                    
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
                        Speed = flightStatus.Value.GroundSpeed,
                        RPM = flightStatus.Value.RPM,
                        AngleOfAttack = flightStatus.Value.AngleOfAttack,
                        AngleOfSideslip = flightStatus.Value.AngleOfSideslip,
                        AirSpeedTrue = flightStatus.Value.AirSpeedTrue
                    };

                    string jsonString = JsonConvert.SerializeObject(telemetryData);

                    //Console.WriteLine(jsonString);

           
                    Byte[] sendBytes = Encoding.ASCII.GetBytes(jsonString);
                    try
                    {
                        udpClient.Send(sendBytes, sendBytes.Length);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.ToString());
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("TelemetryProvider Exception while receiving data:" + e.Message);
            }
        }

        private void Simconnect_OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
        {
            Console.WriteLine("Connected to Flight Simulator");
            if (_simconnect != null)
            {
                _simconnect.RequestDataOnSimObject(DATA_REQUESTS.FLIGHT_STATUS, DEFINITIONS.FlightStatus, 0, SIMCONNECT_PERIOD.SIM_FRAME, SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT, 0, 0, 0);
            }
            else
            {
                Console.WriteLine("SimConnect gone while opening");
            }


        }

        // The case where the user closes Flight Simulator
        private void Simconnect_OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
        {
            Console.WriteLine("Flight Simulator has exited");
            CloseConnection();
        }

        private void Simconnect_OnRecvException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
        {
            Console.WriteLine("Exception received: {error} " + (SIMCONNECT_EXCEPTION)data.dwException);
        }

        #endregion

        private void CloseConnection()
        {
            _connectionClosed = true;
            Console.WriteLine("CloseConnection");
            try
            {
                _cts?.Cancel();
                _cts = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cannot cancel request loop! Error: {ex.Message}");
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
                Console.WriteLine("Cannot unsubscribe events! Error: " + ex.Message);
            }
            IsSimConnectInitialized = false;
        }

        public void GetData()
        {
            _simconnect?.ReceiveMessage();
        }
    }
}
