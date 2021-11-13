using System.Collections.Generic;

namespace BlueDotsServer.Configurations
{
    public class MeshConfigOptions
    {
        public const string SectionName = "MeshConfig";

        public List<OtherServers> OtherServers { get; set; }

    }
}
