using System;
using System.IO.Ports;

namespace GrowattMonitor.Configuration
{
    public class AppConfig
    {
        public string DataloggerReceiverAddress { get; set; }

        public int DataloggerReceiverPort { get; set; }

        public string GrowattServerAddress { get; set; }

        public int GrowattServerPort { get; set; }

        public bool ActAsProxy { get; set; }

        public TimeSpan ReadInterval { get; set; }

        public bool ShowBytesInDump { get; set; } = false;

        public string StorageConnectionstring { get; set; }

        public string TablenamePrefix { get; set; }

        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}

