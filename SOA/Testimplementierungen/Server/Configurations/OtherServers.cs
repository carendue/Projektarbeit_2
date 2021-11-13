using System.Collections.Generic;

namespace BlueDotsServer.Configurations
{
    public class OtherServers
    {
        public string Name { get; set; }

        public string Hostname { get; set; }

        public int PortNr { get; set; }

        public Dictionary<string, int> NetworkQuality { get; set; }
    }
}
