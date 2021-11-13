using Grpc.Core;
using Microsoft.Extensions.Logging;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlueDotsServer.Configurations;
using Google.Protobuf.WellKnownTypes;

namespace BlobsServer
{
    public class BlobsService : Update.UpdateBase
    {
        private static string Host = "localhost";
        private static string User = "postgres";
        private static string DBname = "soa";
        private static string Password = "postgres";
        private static string Port = "5432";
        private readonly ILogger<BlobsService> _logger;
        private List<IServerStreamWriter<blob>> allClients = new();
        private BlobsMeshClient _meshClient;
        private MeshConfigOptions meshConfig;
        private MyName myName;

        public BlobsService(ILogger<BlobsService> logger, BlobsMeshClient meshClient, MeshConfigOptions meshConfig, MyName myName)
        {
            _logger = logger;
            _meshClient = meshClient;
            this.meshConfig = meshConfig;
            this.myName = myName;
        }

        public override async Task NewBlob(IAsyncStreamReader<blob> requestStream, IServerStreamWriter<blob> responseStream, ServerCallContext context)
        {
            allClients.Add(responseStream);
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
                try
                {
                    while (await requestStream.MoveNext())
                    {
                        using (var command = new NpgsqlCommand($"INSERT INTO blobs_{myName.Value} (timestamp, images) VALUES (@t1, @i1)", conn))
                        {
                            var utcTime = requestStream.Current.Timestamp.ToDateTime();
                            var localTime = utcTime.ToLocalTime();
                            //Console.WriteLine("Got a new Blob from one of my clients at {0}.", localTime);
                            Console.WriteLine($"\nGot a new Blob from one of my clients at {DateTime.Now}:{DateTime.Now.Millisecond}.\nTimestamp from the BLOB: {localTime}\n");
                            var image = requestStream.Current.Image;
                            var byteImage = image.ToByteArray();

                            command.Parameters.AddWithValue("t1", utcTime);
                            command.Parameters.AddWithValue("i1", byteImage);
                            command.ExecuteNonQuery();
                        }

                        foreach (var client in allClients.Except(new[] { responseStream }))
                        {
                            await client.WriteAsync(new blob
                            {
                                Image = requestStream.Current.Image,
                                Timestamp = requestStream.Current.Timestamp
                            });
                        }
                        await _meshClient.SendToMesh(requestStream.Current, null);
                    }

                }
                catch (Exception e)
                {
                    _logger.LogError("Something went wrong - Error: {0}", e);
                }
                allClients.Remove(responseStream);
            }

        }

        public override async Task GetHistory(Empty request, IServerStreamWriter<history> responseStream, ServerCallContext context)
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
                using (var command = new NpgsqlCommand($"SELECT timestamp FROM (SELECT timestamp from blobs_{myName.Value} ORDER BY timestamp DESC LIMIT 100) AS rightorder ORDER BY timestamp ASC", conn))
                {
                    var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var msg = $"New Blob at {reader[0]}.";
                        await responseStream.WriteAsync(new history
                        {
                            History = msg
                        });
                    }
                }
                conn.Close();
            }
        }

        public List<IServerStreamWriter<blob>> getAllClients()
        {
            return allClients;
        }


    }
}
