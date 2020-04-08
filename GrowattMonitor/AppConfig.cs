using System;
using System.IO.Ports;

namespace GrowattMonitor.Configuration
{
     public class CosmosDBSettings
    {
        public string EndpointUrl { get; set; }
        public string AuthorizationKey { get; set; }
        public string Database { get; set; }
        public string Container { get; set; }
    }

    public class AppConfig
    {
        public string DataloggerReceiverAddress { get; set; }
        public string GrowattServerAddress { get; set; }

        public bool ActAsProxy { get; set; }

        public TimeSpan ReadInterval { get; set; }
        public string DataFilesPath { get; set; }
        public bool SaveDataFiles { get; set; }
        public CosmosDBSettings CosmosDBSettings { get; set; }

        public double Latitude { get; set; }
        public double Longitude { get; set; }

        public string[] Ids { get; set; } = null;
    }
}

