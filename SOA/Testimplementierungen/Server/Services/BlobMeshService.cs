using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlueDotsServer.Configurations;
using Npgsql;

namespace BlobsServer.Services
{
    public class BlobMeshService : MeshUpdate.MeshUpdateBase
    {
        private static string Host = "localhost";
        private static string User = "postgres";
        private static string DBname = "soa";
        private static string Password = "postgres";
        private static string Port = "5432";
        private MeshConfigOptions otherServers;
        private BlobsMeshClient _meshClient;
        private MyName _myName;
        private readonly BlobsService blobsService;
        public string nameOfSender;


        public BlobMeshService(MeshConfigOptions otherServers, BlobsMeshClient meshClient, MyName myName, BlobsService blobsService)
        {
            this.otherServers = otherServers;
            _meshClient = meshClient;
            _myName = myName;
            this.blobsService = blobsService;
        }

        public override async Task<Empty> NewBlob(IAsyncStreamReader<blob> requestStream, ServerCallContext context)
        {
            String connString =
                String.Format(
                    "Server={0};Username={1};Database={2};Port={3};Password={4};SSLMode=Prefer",
                    Host,
                    User,
                    DBname,
                    Port,
                    Password);

            using (var conn = new NpgsqlConnection(connString)) 
            {
                conn.Open();

                nameOfSender = context.RequestHeaders.GetValue("myname");
                var myClients = blobsService.getAllClients();

                while (await requestStream.MoveNext())
                {
                    Console.WriteLine($"\nI received a new Blob from {nameOfSender} at {DateTime.Now}:{DateTime.Now.Millisecond}.\n");

                    using (var command = new NpgsqlCommand($"INSERT INTO blobs_{_myName.Value} (timestamp, images) VALUES (@t1, @i1)", conn))
                    {
                        var utcTime = requestStream.Current.Timestamp.ToDateTime();
                        var localTime = utcTime.ToLocalTime();
                        var image = requestStream.Current.Image;
                        var byteImage = image.ToByteArray();

                        command.Parameters.AddWithValue("t1", utcTime);
                        command.Parameters.AddWithValue("i1", byteImage);
                        command.ExecuteNonQuery();
                    }

                    var msg = $"\nNew Blob delivered from {nameOfSender} at Timestamp: {requestStream.Current.Timestamp.ToDateTime().ToLocalTime()}!\n";
                    Console.WriteLine(msg);

                    foreach (var client in myClients)
                    {
                        await client.WriteAsync(new blob
                        {
                            Timestamp = requestStream.Current.Timestamp,
                            Image = requestStream.Current.Image
                        });
                    }

                    var networkQualities = GetSenderNetworkQualities();
                    foreach (var server in otherServers.OtherServers)
                    {
                        networkQualities.TryGetValue(server.Name, out var nq);
                        if (nq < 70 && server.Name != _myName.Value && server.Name != nameOfSender && requestStream.Current.CalculateSize() > 100)
                        {
                            await _meshClient.SendToMesh(new blob { Image = requestStream.Current.Image, Timestamp = requestStream.Current.Timestamp }, nameOfSender);
                        }
                    }

                }
            }
            
            return new Empty { };
        }

        private Dictionary<string, int> GetSenderNetworkQualities()
        {
            var myConfig = otherServers.OtherServers.First(conf => conf.Name == nameOfSender);
            return myConfig.NetworkQuality;
        }
    }
}
