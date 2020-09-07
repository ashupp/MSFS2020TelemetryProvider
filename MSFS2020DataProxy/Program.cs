using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.FlightSimulator.SimConnect;
using SimFeedback.telemetry;

namespace MSFS2020DataProxy
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Starting Proxy");
            var simConnectDataGrabber = new SimConnectDataGrabber();
            simConnectDataGrabber.InitializeSimConnect();

            while(simConnectDataGrabber.IsSimConnectInitialized)
            {
                simConnectDataGrabber.GetData();
            }
        }



       
    }
}
