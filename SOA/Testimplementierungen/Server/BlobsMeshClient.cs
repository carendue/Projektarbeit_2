using Grpc.Core;
using Grpc.Net.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlueDotsServer.Configurations;
using Google.Protobuf.WellKnownTypes;
//using System.Diagnostics;

namespace BlobsServer
{

    public class BlobsMeshClient
    {
        private static List<MeshStream> meshClients = new();
        private MeshConfigOptions otherServers;
        private MyName myName;

        public BlobsMeshClient(MeshConfigOptions otherServers, MyName myName)
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
                    var newBlobStream = meshClient.NewBlob(metadata);
                    meshClients.Add(new MeshStream { Name = server.Name, Stream = newBlobStream.RequestStream });
                }
            }

        }

        private Dictionary<string, int> GetNetworkQualities()
        {
            var myConfig = otherServers.OtherServers.First(conf => conf.Name == myName.Value);
            return myConfig.NetworkQuality;
        }

        public async Task SendToMesh(blob blob, string nameOfSender)
        {
            var time = blob.Timestamp;
            var image = blob.Image;
            
            foreach (var client in meshClients)
            {
                var networkQualities = GetNetworkQualities();
                networkQualities.TryGetValue(client.Name, out var netQ);

                try
                {
                    if ((netQ > 70 || blob.CalculateSize() < 100) && client.Name != nameOfSender)
                    {
                        await client.Stream.WriteAsync(new blob
                        {
                            Timestamp = time,
                            Image = image
                        });
                    }
                }
                catch (Exception)
                {
                    Console.WriteLine($"Sending Blob to server {client.Name} failed. Server {client.Name} is not accessible.");
                }
            }
        }

        private class MeshStream
        {
            public string Name { get; set; }

            public IClientStreamWriter<blob> Stream { get; set; }
        }
    }
}
