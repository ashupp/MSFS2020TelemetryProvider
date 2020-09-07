using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using SimFeedback.log;
using SimFeedback.telemetry.Properties;

namespace SimFeedback.telemetry
{
    public sealed class TelemetryProvider : AbstractTelemetryProvider
    {
        #region Members

        private bool _isStopped = true;
        private bool _connectionClosed;

        private readonly int _fixedRateLimiter = 1000 / Settings.Default.TelemetryUpdateFrequency;
        private readonly bool _autoCalculateRateLimiter = Settings.Default.AutoCalculateRateLimiter;

        private const int WM_USER_SIMCONNECT = 0x0402;
        private bool _simConnectInitialized;
        private CancellationTokenSource _cts;
        private Thread _t;
        private TelemetryData _lastTelemetryData;
        private IntPtr _mainWindowHandle;

        private const int _portNum = 55280;
        private const string _ipAddr = "127.0.0.1";

        #endregion

        #region Public methods

        public TelemetryProvider()
        {
            Author = "ashupp / ashnet GmbH";
            Version = Assembly.LoadFrom(Assembly.GetExecutingAssembly().Location).GetName().Version.ToString();
            BannerImage = @"img\banner_" + Name + ".png";
            IconImage = @"img\icon_" + Name + ".png";
            TelemetryUpdateFrequency = Settings.Default.TelemetryUpdateFrequency;
        }

        public override string Name => "msfs2020";

        public override void Init(ILogger logger)
        {
            base.Init(logger);
            Log("Initializing " + Name + "TelemetryProvider " + Version);
            LogDebug("TelemetryUpdateFrequency: " + TelemetryUpdateFrequency);
            LogDebug("SamplePeriod: " + SamplePeriod);
            LogDebug("AutoCalculateRateLimiter: " + _autoCalculateRateLimiter);
            LogDebug("FixedRateLimiter: " + _fixedRateLimiter);
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


        private void Run()
        {
            _lastTelemetryData = new TelemetryData();

            UdpClient socket = new UdpClient { ExclusiveAddressUse = false };
            socket.Client.Bind(new IPEndPoint(IPAddress.Parse(_ipAddr), _portNum));
            var endpoint = new IPEndPoint(IPAddress.Parse(_ipAddr), _portNum);
            Stopwatch sw = new Stopwatch();
            sw.Start();
            while (!_isStopped)
            {
                try
                {

                    // get data from game, 
                    if (socket.Available == 0)
                    {
                        if (sw.ElapsedMilliseconds > 500)
                        {
                            IsRunning = false;
                            IsConnected = false;
                            Thread.Sleep(1000);
                        }
                        continue;
                    }
                    IsConnected = true;
                    IsRunning = true;
                    Byte[] received = socket.Receive(ref endpoint);
                    string resp = Encoding.ASCII.GetString(received);
                    TelemetryData telemetryData = JsonConvert.DeserializeObject<TelemetryData>(resp);

                    TelemetryEventArgs args = new TelemetryEventArgs(new TelemetryInfoElem(telemetryData, _lastTelemetryData));
                    RaiseEvent(OnTelemetryUpdate, args);
                    _lastTelemetryData = telemetryData;
                    sw.Restart();

                }
                catch (Exception e)
                {
                    LogError(Name + " Exception while processing data", e);
                    IsConnected = false;
                    IsRunning = false;
                    Thread.Sleep(1000);
                }
            }
            sw.Stop();
            IsConnected = false;
            IsRunning = false;
        }
        #endregion
    }
}