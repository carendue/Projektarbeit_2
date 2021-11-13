using BlueDotsServer.Configurations;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using System;
using System.Collections.Generic;
//using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace BlueDotsServer
{

    public class MeshClient
    {
        private static List<MeshStream> meshClients = new();
        private MeshConfigOptions otherServers;
        private MyName myName;

        public MeshClient(MeshConfigOptions otherServers, MyName myName)
        {
            this.otherServers = otherServers;
            this.myName = myName;

            Console.WriteLine("I am server {0}.", myName.Value);            
            foreach (var server in otherServers.OtherServers)
            {                
                if (server.Name != myName.Value)
                {
                    var otherServerChannel = GrpcChannel.ForAddress($"https://{server.Hostname}:{server.PortNr}");
                    var meshClient = new MeshUpdate.MeshUpdateClient(otherServerChannel);
                    var metadata = new Metadata { { "myname", myName.Value } };
                    var newBluedotStream = meshClient.NewBlueDot(metadata);
                    meshClients.Add(new MeshStream { Name = server.Name, Stream = newBluedotStream.RequestStream });
                }
            }

        }

        private Dictionary<string, int> GetNetworkQualities()
        {
            var myConfig = otherServers.OtherServers.First(conf => conf.Name == myName.Value);
            return myConfig.NetworkQuality;
        }

        public async Task SendToMesh(bluedot dot, string nameOfSender)
        {
            var time = dot.Timestamp;
            var location = dot.Geolocation;
            var id = dot.UnitID;

            foreach (var client in meshClients)
            {
                var networkQualities = GetNetworkQualities();
                networkQualities.TryGetValue(client.Name, out var netQ);
                try
                {
                    if ((netQ > 70 || dot.CalculateSize() < 100) && client.Name != nameOfSender)
                    {
                        await client.Stream.WriteAsync(new bluedot
                        {
                            Timestamp = time,
                            Geolocation = location,
                            UnitID = id
                        });
                    }

                }
                catch (Exception)
                {
                    Console.WriteLine($"Sending BlueDot to server {client.Name} failed. Server {client.Name} is not accessible.");
                }
            }

        }

        private class MeshStream
        {
            public string Name { get; set; }

            public IClientStreamWriter<bluedot> Stream { get; set; }
        }
    }
}
