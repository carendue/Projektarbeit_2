using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace BlobsProducer
{
    class BlobsProducer
    {
        
        static void Main(string[] args)
        {
            Byte[] byteArray = Array.Empty<Byte>();
            var _myName = Environment.GetEnvironmentVariable("MY_NAME") ?? "A";
            Console.WriteLine("MY SERVER SHOULD BE: " + _myName);

            var factory = new ConnectionFactory();
            factory.UserName = "caren";
            factory.Password = "caren";
            factory.VirtualHost = "/";
            factory.HostName = "caren-pc";
            factory.Port = AmqpTcpEndpoint.UseDefaultPort;

            var timerA = new Stopwatch();
            var timerB = new Stopwatch();
            var timerC = new Stopwatch();

            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                channel.ExchangeDeclare(exchange: "hello", type: "direct");
                var msg = "Hello, I'm new!";
                var bod = Encoding.UTF8.GetBytes(msg);
                var props = channel.CreateBasicProperties();
                Guid guid = Guid.NewGuid();
                var strGuid = Convert.ToString(guid);
                props.MessageId = strGuid;
                var rK = _myName + strGuid;

                var historyQueue = channel.QueueDeclare().QueueName;
                channel.QueueBind(queue: historyQueue, exchange: "history", routingKey: rK);
                Console.WriteLine("History:\n----------------------------------------------------------------------------------------------------");

                var historyEntriesReceived = 0;
                using var allHistoryReceived = new ManualResetEvent(false);
                var cons = new EventingBasicConsumer(channel);

                cons.Received += (model, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    Console.WriteLine(message);
                    if (string.IsNullOrEmpty(message))
                    {
                        allHistoryReceived.Set();
                    }
                    else
                    {
                        ++historyEntriesReceived;

                    }
                };
                channel.BasicConsume(queue: historyQueue, autoAck: true, consumer: cons);
                channel.BasicPublish(exchange: "hello", routingKey: _myName, basicProperties: props, body: bod);
                allHistoryReceived.WaitOne();
                Console.WriteLine($"Received {historyEntriesReceived} history entries");
                Console.WriteLine("----------------------------------------------------------------------------------------------------\n");

                channel.ExchangeDeclare(exchange: "blobs", type: ExchangeType.Fanout);
                Guid clientID = Guid.NewGuid();
                var myName = clientID.ToString();           
                var consumer = new EventingBasicConsumer(channel);
                var queueName = channel.QueueDeclare().QueueName;

                channel.QueueBind(
                    queue: queueName,
                    exchange: "blobs",
                    routingKey: "");

                consumer.Received += (model, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var timestampBytes = body.Take(sizeof(long)).ToArray();
                    var timestampTicks = BitConverter.ToInt64(timestampBytes);
                    var timestamp = new DateTime(timestampTicks, DateTimeKind.Utc);
                    var localtime = timestamp.ToLocalTime();
                    var imgBytes = body.Skip(sizeof(long)).ToArray();
                    var headers = ea.BasicProperties.Headers.TryGetValue("Sender", out var senderName);
                    var name = Encoding.UTF8.GetString((byte[])senderName);
                    
                    if (name != myName)
                    {
                        Console.WriteLine($"\nReceived a Blob from {name} at {DateTime.Now}:{DateTime.Now.Millisecond}.\n");
                        Console.WriteLine("[x] New Blob at {0}.", localtime);
                    }

                };

                var acksQueue = channel.QueueDeclare().QueueName;
                channel.QueueBind(queue: acksQueue, exchange: "acks", routingKey: strGuid);
                var acksCons = new EventingBasicConsumer(channel);
                acksCons.Received += (model, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var msg = Encoding.UTF8.GetString(body);
                    var ackConsumer = ea.BasicProperties.MessageId;
                    if (ackConsumer == "A")
                    {
                        timerA.Stop();
                        var timeUntilA = timerA.ElapsedMilliseconds;
                        Console.WriteLine($"{timeUntilA} ms until we received the ack from A.");
                    }
                    else if (ackConsumer == "B")
                    {
                        timerB.Stop();
                        var timeUntilB = timerB.ElapsedMilliseconds;
                        Console.WriteLine($"{timeUntilB} ms until we received the ack from B.");
                    }
                    else if (ackConsumer == "C")
                    {
                        timerC.Stop();
                        var timeUntilC = timerC.ElapsedMilliseconds;
                        Console.WriteLine($"{timeUntilC} ms until we received the ack from C.");
                    }

                };

                while (true)
                {
                    channel.BasicConsume(acksQueue, true, acksCons);

                    channel.BasicConsume(
                    queue: queueName,
                    autoAck: true,
                    consumer: consumer);

                    var timestamp = new DateTime();
                    timestamp = DateTime.UtcNow;
                    var localtime = timestamp.ToLocalTime();
                    var img = @"C:\Users\A764843\Downloads\4kfoto.jpg";
                    var imgBytes = File.ReadAllBytes(img);

                    var rnd = new Random();
                    var timestampBytes = BitConverter.GetBytes(timestamp.Ticks);
                    var body = timestampBytes.Concat(imgBytes).ToArray();

                    var properties = channel.CreateBasicProperties();
                    properties.Headers = new Dictionary<string, object>();
                    properties.Headers.Add("Sender", myName);
                    properties.MessageId = strGuid;

                    try
                    {
                        Console.WriteLine($"\nSending a new Blob at {DateTime.Now}:{DateTime.Now.Millisecond}.\n");
                        timerA.Start();
                        timerB.Start();
                        timerC.Start();
                        channel.BasicPublish(
                        exchange: "blobs",
                        routingKey: "",
                        basicProperties: properties,
                        body: body);
                        Console.WriteLine($"Finished sending the Blob at {DateTime.Now}:{DateTime.Now.Millisecond}.\n");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }

                    Console.WriteLine("[x] Sent Blob at {0}.", localtime);

                    // Blobs sollen im Zyklus von 5 min bis 60 min erstellt werden (= 300.000 bis 3.600.000 ms)
                    //var cyclic = rnd.Next(300000, 3600000);
                    // Um die Durchführung der Messungen zu vereinfachen, wurde der Zyklus auf 1 - 3 min angepasst (= 60.000 bis 180.000 ms)
                    var cyclic = rnd.Next(60000, 180000);
                    Console.WriteLine(cyclic / 60000 + " minutes and " + (cyclic % 60000) / 1000 + " seconds until the next blob.\n");
                    Thread.Sleep(cyclic);
                }                
            }
        }
    }
}
