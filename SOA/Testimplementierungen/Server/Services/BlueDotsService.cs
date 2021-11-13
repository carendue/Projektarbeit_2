using BlueDotsServer.Configurations;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;

namespace BlueDotsServer
{
    public class BlueDotsService : Update.UpdateBase
    {
        private static string Host = "localhost";
        private static string User = "postgres";
        private static string DBname = "soa";
        private static string Password = "postgres";
        private static string Port = "5432";
        private readonly ILogger<BlueDotsService> _logger;
        private List<IServerStreamWriter<bluedot>> allClients = new();
        private MeshClient _meshClient;
        private MeshConfigOptions meshConfig;
        private MyName myName;

        public BlueDotsService(ILogger<BlueDotsService> logger, MeshClient meshClient, MeshConfigOptions meshConfig, MyName myName)
        {
            _logger = logger;
            _meshClient = meshClient;
            this.meshConfig = meshConfig;
            this.myName = myName;
        }

        public override async Task NewBlueDot(IAsyncStreamReader<bluedot> requestStream, IServerStreamWriter<bluedot> responseStream, ServerCallContext context)
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
                        using (var command = new NpgsqlCommand($"INSERT INTO bluedots_{myName.Value} (timestamp, geolocation, unitid) VALUES (@t1, @g1, @u1)", conn))
                        {
                            var time = requestStream.Current.Timestamp.ToDateTime();
                            var localtime = time.ToLocalTime();
                            //Console.WriteLine("Got a new BlueDot from one of my clients at {0}.", localtime);
                            Console.WriteLine($"\nGot a new BlueDot from one of my clients at {DateTime.Now}:{DateTime.Now.Millisecond}.\nID: {requestStream.Current.UnitID}\n");
                            var latitude = requestStream.Current.Geolocation.Latitude;
                            var longitude = requestStream.Current.Geolocation.Longitutde;
                            NpgsqlPoint geolocation = new(latitude, longitude);
                            Guid unitID = new(requestStream.Current.UnitID);

                            command.Parameters.AddWithValue("t1", time);
                            command.Parameters.AddWithValue("g1", geolocation);
                            command.Parameters.AddWithValue("u1", unitID);
                            command.ExecuteNonQuery();
                        }

                        foreach (var client in allClients.Except(new[] { responseStream }))
                        {
                            await client.WriteAsync(new bluedot
                            {
                                Timestamp = requestStream.Current.Timestamp,
                                Geolocation = requestStream.Current.Geolocation,
                                UnitID = requestStream.Current.UnitID
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
                using (var command = new NpgsqlCommand($"SELECT * FROM (SELECT * FROM bluedots_{myName.Value} ORDER BY timestamp DESC LIMIT 100) AS rightorder ORDER BY timestamp ASC", conn))
                {
                    var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        //var timestamp = reader[0];
                        var msg = $"{reader[0]}\t{reader[1]}\t{reader[2]}";
                        await responseStream.WriteAsync(new history
                        {
                            History = msg
                        });
                    }
                }
                conn.Close();
            } 
        }

        public List<IServerStreamWriter<bluedot>> getAllClients()
        {            
            return allClients;
        }
    }
}
