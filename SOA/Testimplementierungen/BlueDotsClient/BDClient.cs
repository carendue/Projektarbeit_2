using BlueDotsServer;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using System;
//using System.Diagnostics;
using System.Threading.Tasks;

namespace BlueDotsClient
{
    class BDClient
    {
        public static async Task Main(string[] args)
        {
            var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
            var myName = Environment.GetEnvironmentVariable("MY_NAME") ?? "A";
            var hostname = configuration.GetValue<string>($"Server:{myName}:Host") ?? "localhost";
            var port = configuration.GetValue<int?>($"Server:{myName}:Port") ?? 5001;
            using var channel = GrpcChannel.ForAddress($"https://{hostname}:{port}");

            await GetHistory(channel);

            var client = new Update.UpdateClient(channel);
            using var stream = client.NewBlueDot();

            var response = Task.Run(async () =>
            {
                await foreach (var rm in stream.ResponseStream.ReadAllAsync())
                {
                    //Console.WriteLine($"\nI received a BlueDot at {DateTime.Now}:{DateTime.Now.Millisecond}.\nID: {rm.UnitID}\n");
                    Console.WriteLine($"\nNew BlueDot! \nUnitID: {rm.UnitID}\nGeolocation: {rm.Geolocation.Latitude}, {rm.Geolocation.Longitutde}\nTimestamp: {rm.Timestamp.ToDateTime().ToLocalTime()}");
                }
            });

            // Codeanpassungen für die Messung von 1.000 Blue Dots

            //Console.WriteLine($"I'm starting to send 1.000 Blue Dots. It's {DateTime.Now}:{DateTime.Now.Millisecond}");
            //for (int i = 0; i < 1000; i++)
            //{
            //    Console.WriteLine(i);
            //    var time = new DateTime();
            //    time = DateTime.UtcNow;
            //    var timestamp = Timestamp.FromDateTime(time);
            //    var rnd = new Random();
            //    var doubleLongitude = rnd.NextDouble();
            //    var doubleLatitude = rnd.NextDouble();
            //    double latitude = doubleLatitude + rnd.Next(45, 55);
            //    double longitude = doubleLongitude + rnd.Next(5, 15);
            //    Geolocation geolocation = new Geolocation { Latitude = latitude, Longitutde = longitude };
            //    Guid unit = Guid.NewGuid();
            //    var unitID = unit.ToString();

            //    try
            //    {
            //        await stream.RequestStream.WriteAsync(new bluedot
            //        {
            //            Timestamp = timestamp,
            //            Geolocation = geolocation,
            //            UnitID = unitID
            //        });
            //    }
            //    catch (Exception e)
            //    {
            //        Console.WriteLine(e);
            //    }                
            //}
            //Console.WriteLine("Finished sending 1.000 BlueDots. It's " + DateTime.Now + ":" + DateTime.Now.Millisecond + "\n Press enter to exit.");
            //Console.ReadLine();

            while (true)
            {

                var time = new DateTime();
                time = DateTime.UtcNow;
                var timestamp = Timestamp.FromDateTime(time);
                var rnd = new Random();
                var doubleLongitude = rnd.NextDouble();
                var doubleLatitude = rnd.NextDouble();
                double latitude = doubleLatitude + rnd.Next(45, 55);
                double longitude = doubleLongitude + rnd.Next(5, 15);
                Geolocation geolocation = new Geolocation { Latitude = latitude, Longitutde = longitude };
                Guid unit = Guid.NewGuid();
                var unitID = unit.ToString();
                try
                {
                    Console.WriteLine($"I am sending a new BlueDot at {DateTime.Now}:{DateTime.Now.Millisecond}.\nID: {unitID}\n");
                    await stream.RequestStream.WriteAsync(new bluedot
                    {
                        Timestamp = timestamp,
                        Geolocation = geolocation,
                        UnitID = unitID
                    });
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }

                //Blue Dots sollen im Zyklus von 1 sek bis 5 min erstellt werden(= 1.000 bis 300.000 ms)
                //var cyclic = rnd.Next(1000, 300000);
                var cyclic = rnd.Next(60000, 180000);
                Console.WriteLine(cyclic / 60000 + " Minuten und " + (cyclic % 60000) / 1000 + " Sekunden bis zum nächsten BlueDot.");
                await Task.Delay(cyclic);
            }
        }

        public static async Task GetHistory(GrpcChannel channel)
        {
            var client = new Update.UpdateClient(channel);
            try
            {
                using (var call = client.GetHistory(new Empty())) 
                {
                    var responseStream = call.ResponseStream;
                    Console.WriteLine("History:");
                    Console.WriteLine("----------------------------------------------------------------------------------------------------");
                    Console.WriteLine("Timestamp:\t\tGeolocation:\t\t\t\tUnitID:");
                    while (await responseStream.MoveNext())
                    {
                        var history = responseStream.Current.History;
                        Console.WriteLine(history);
                    }
                    Console.WriteLine("----------------------------------------------------------------------------------------------------\n");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: {0}", e);
            }
        }
    }
}
