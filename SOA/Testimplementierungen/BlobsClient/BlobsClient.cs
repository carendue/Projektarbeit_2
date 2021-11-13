using BlobsServer;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
//using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace BlobsClient
{
    class BlobsClient
    {        
        public static async Task Main(string[] args)
        {
            var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
            var myName = Environment.GetEnvironmentVariable("MY_NAME") ?? "A";
            var hostname = configuration.GetValue<string>($"Server:{myName}:Host") ?? "localhost";
            var port = configuration.GetValue<int?>($"Server:{myName}:Port") ?? 5001;
            using var channel = GrpcChannel.ForAddress($"https://{hostname}:{port}", new GrpcChannelOptions
            {
                MaxReceiveMessageSize = 6 * 1024 * 1024 // 6 MB
            });

            await GetHistory(channel);

            var client = new Update.UpdateClient(channel);
            using var stream = client.NewBlob();

            var response = Task.Run(async () =>
            {
                await foreach (var rm in stream.ResponseStream.ReadAllAsync())
                {
                    //Console.WriteLine($"\nI received a Blob at {DateTime.Now}:{DateTime.Now.Millisecond}.\n");
                    Console.WriteLine($"\nNew Blob at Timestamp: {rm.Timestamp.ToDateTime().ToLocalTime()}!");
                }
            });

            while (true)
            {
                var time = new DateTime();
                time = DateTime.UtcNow;
                var timestamp = Timestamp.FromDateTime(time);
                var img = @"C:\Users\A764843\Downloads\4kfoto.jpg";
                var bytes = await File.ReadAllBytesAsync(img);
                var grpcBytes = ByteString.CopyFrom(bytes);
                var rnd = new Random();

                try
                {
                    Console.WriteLine($"I am sending a new Blob at {DateTime.Now}:{DateTime.Now.Millisecond}.\n");
                    await stream.RequestStream.WriteAsync(new blob
                    {
                        Timestamp = timestamp,
                        Image = grpcBytes
                    });
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }

                // Blobs sollen im Zyklus von 5 min bis 60 min erstellt werden (= 300.000 bis 3.600.000 ms)
                //var cyclic = rnd.Next(300000, 3600000);
                var cyclic = rnd.Next(60000, 180000);
                Console.WriteLine(cyclic / 60000 + " Minuten und " + (cyclic % 60000) / 1000 + " Sekunden bis zum nächsten Blob.");
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
                    Console.WriteLine("--------------------------------");
                    while (await responseStream.MoveNext())
                    {
                        var history = responseStream.Current.History;
                        Console.WriteLine(history);
                    }
                    Console.WriteLine("--------------------------------\n");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: {0}", e);
            }
        }
    }
}
