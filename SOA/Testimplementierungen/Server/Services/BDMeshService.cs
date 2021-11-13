using BlueDotsServer.Configurations;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;

namespace BlueDotsServer.Services
{
    public class MeshService : MeshUpdate.MeshUpdateBase
    {
        private static string Host = "localhost";
        private static string User = "postgres";
        private static string DBname = "soa";
        private static string Password = "postgres";
        private static string Port = "5432";
        private MeshConfigOptions otherServers;
        private MeshClient _meshClient;
        private MyName _myName;
        private readonly BlueDotsService blueDotsService;
        public string nameOfSender;

        public MeshService(MeshConfigOptions otherServers, MeshClient meshClient, MyName myName, BlueDotsService blueDotsService)
        {
            this.otherServers = otherServers;
            _meshClient = meshClient;
            _myName = myName;
            this.blueDotsService = blueDotsService;
        }

        public override async Task<Empty> NewBlueDot(IAsyncStreamReader<bluedot> requestStream, ServerCallContext context)
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
                var myClients = blueDotsService.getAllClients();

                while (await requestStream.MoveNext())
                {
                    Console.WriteLine($"\nI received a new BlueDot from {nameOfSender} at {DateTime.Now}:{DateTime.Now.Millisecond}.\nID: {requestStream.Current.UnitID}\n");
                    using (var command = new NpgsqlCommand($"INSERT INTO bluedots_{_myName.Value} (timestamp, geolocation, unitid) VALUES (@t1, @g1, @u1)", conn))
                    {
                        var time = requestStream.Current.Timestamp.ToDateTime();
                        var localtime = time.ToLocalTime();
                        var latitude = requestStream.Current.Geolocation.Latitude;
                        var longitude = requestStream.Current.Geolocation.Longitutde;
                        NpgsqlPoint geolocation = new(latitude, longitude);
                        Guid unitID = new(requestStream.Current.UnitID);

                        command.Parameters.AddWithValue("t1", time);
                        command.Parameters.AddWithValue("g1", geolocation);
                        command.Parameters.AddWithValue("u1", unitID);
                        command.ExecuteNonQuery();
                    }

                    var msg = $"\nNew BlueDot delivered from {nameOfSender}! \nUnitID: {requestStream.Current.UnitID}\nGeolocation: {requestStream.Current.Geolocation.Latitude}, {requestStream.Current.Geolocation.Longitutde}\nTimestamp: {requestStream.Current.Timestamp.ToDateTime().ToLocalTime()}\n";
                    Console.WriteLine(msg);

                    foreach (var client in myClients)
                    {
                        await client.WriteAsync(new bluedot
                        {
                            Timestamp = requestStream.Current.Timestamp,
                            Geolocation = requestStream.Current.Geolocation,
                            UnitID = requestStream.Current.UnitID
                        });
                    }

                    var networkQualities = GetSenderNetworkQualities();
                    foreach (var server in otherServers.OtherServers)
                    {
                        networkQualities.TryGetValue(server.Name, out var nq);
                        if (nq < 70 && server.Name != _myName.Value && server.Name != nameOfSender && requestStream.Current.CalculateSize() > 100)
                        {
                            await _meshClient.SendToMesh(new bluedot { Timestamp = requestStream.Current.Timestamp, Geolocation = requestStream.Current.Geolocation, UnitID = requestStream.Current.UnitID }, nameOfSender);
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
