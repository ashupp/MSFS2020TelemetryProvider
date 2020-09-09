using System;
using System.Diagnostics;
using System.IO;
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

        private readonly int _telemetryUpdateFrequency = Settings.Default.TelemetryUpdateFrequency;
        private readonly int _fixedRateLimiter = 1000 / Settings.Default.TelemetryUpdateFrequency;
        private readonly bool _autoCalculateRateLimiter = Settings.Default.AutoCalculateRateLimiter;
        private readonly bool _disableRateThrottle = Settings.Default.DisableRateThrottle;
        private readonly bool _showProxyWindow = Settings.Default.ShowProxyWindow;

        private bool _isStopped = true;

        private Thread _t;
        private bool _proxyRunning;
        private Process _proxyProcess;
        private TelemetryData _lastTelemetryData;
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

        private void TryStartProxy()
        {
            try
            {
                _proxyRunning = true;
                var proxyDir = Path.Combine(Directory.GetCurrentDirectory(), "provider", "MSFS2020TelemetryProviderProxy");
                var proxyPath = Path.Combine(proxyDir,"MSFS2020DataProxy.exe");

                LogDebug("proxy path: " + proxyPath);
                var startInfo = new ProcessStartInfo(proxyPath);
                startInfo.WorkingDirectory = proxyDir;
                startInfo.CreateNoWindow = !_showProxyWindow;
                startInfo.UseShellExecute = false;
                startInfo.RedirectStandardError = !_showProxyWindow;
                startInfo.RedirectStandardInput = !_showProxyWindow;
                startInfo.RedirectStandardOutput = !_showProxyWindow;

                _proxyProcess = Process.Start(startInfo);
                if (_proxyProcess != null)
                {
                    _proxyProcess.EnableRaisingEvents = true;
                    _proxyProcess.ErrorDataReceived += _proxyProcess_ErrorDataReceived;
                    _proxyProcess.OutputDataReceived += _proxyProcess_OutputDataReceived;
                    _proxyProcess.Exited += Proc_Exited;
                    
                }
            }
            catch (Exception e)
            {
                LogError("ProxyHelper: Exception: " + e.Message);
            }
        }

        private void _proxyProcess_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            Log("ProxyHelper OutputDataReceived: " + e.Data);
        }

        private void _proxyProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            Log("ProxyHelper ErrorDataReceived: " + e.Data);
        }

        private void Proc_Exited(object sender, EventArgs e)
        {
            Log("ProxyHelper Exited - Exit Code: " + _proxyProcess.ExitCode);
            _proxyProcess.Exited -= Proc_Exited;
            _proxyProcess.ErrorDataReceived -= _proxyProcess_ErrorDataReceived;
            _proxyProcess.OutputDataReceived -= _proxyProcess_OutputDataReceived;
            _proxyProcess.Dispose();
            _proxyRunning = false;
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
                    if (!_proxyRunning)
                    {
                        _proxyRunning = true;
                        TryStartProxy();
                    }

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

                    if (!_disableRateThrottle)
                    {
                        if (_autoCalculateRateLimiter)
                        {
                            var sleepMs = (int)(1000 / _telemetryUpdateFrequency - sw.ElapsedMilliseconds);
                            if (sleepMs > 0) Thread.Sleep(sleepMs);
                        }
                        else
                        {
                            Thread.Sleep(_fixedRateLimiter);
                        }
                    }
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

            if (_proxyRunning)
            {
                Log("Stopping Proxy");
                _proxyProcess?.CloseMainWindow();
            }
            else
            {
                Log("Proxy not running...");
            }
            socket.Dispose();
            _proxyProcess?.Dispose();
            sw.Stop();
            IsConnected = false;
            IsRunning = false;
            _proxyRunning = false;
        }
        #endregion
    }
}