using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

namespace BDsProducer
{
    class BDsProducer
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

            var timer = new Stopwatch();
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
                Console.WriteLine("Timestamp\t\tGeolocation\t\t\t\tUnitID");

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

                channel.ExchangeDeclare(exchange: "bluedots", type: ExchangeType.Fanout);
                Guid clientID = Guid.NewGuid();
                var myName = clientID.ToString();
                var consumer = new EventingBasicConsumer(channel);
                var queueName = channel.QueueDeclare().QueueName;

                channel.QueueBind(
                    queue: queueName,
                    exchange: "bluedots",
                    routingKey: "");

                consumer.Received += (model, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    var time = message.Split('/')[0];
                    var localtime = Convert.ToDateTime(time).ToLocalTime();

                    var latitude = Convert.ToDouble(message.Split('/')[1]);
                    var longitude = Convert.ToDouble(message.Split('/')[2]);
                    var unitID = message.Split('/')[3];
                    var headers = ea.BasicProperties.Headers.TryGetValue("Sender", out var senderName);
                    var name = Encoding.UTF8.GetString((byte[])senderName);

                    if (name != myName)
                    {                        
                        Console.WriteLine($"\nReceived a BlueDot from {name} at {DateTime.Now}:{DateTime.Now.Millisecond}.\nID: {unitID}\n");
                        Console.WriteLine("[x] New BlueDot!\nTimestamp: {0}\nGeolocation: {1}, {2}\nUnitID: {3}\n", localtime, latitude, longitude, unitID);
                    }

                };

                var acksQueue = channel.QueueDeclare().QueueName;
                channel.QueueBind(queue: acksQueue, exchange: "acks", routingKey: strGuid);
                var acksCons = new EventingBasicConsumer(channel);

                // Counter die für die Zählung der 1.000 Blue Dots benötigt werden
                var Actr = 0;
                var Bctr = 0;
                var Cctr = 0;

                acksCons.Received += (model, ea) =>
                {
                    // Codeanpassungen für die Durchführung der Messung von 1.000 zu übertragenden Blue Dots
                    var ackConsumer = ea.BasicProperties.MessageId;
                    if (ackConsumer == "A")
                    {
                        Actr++;
                        if (Actr == 1000)
                        {
                            timerA.Stop();
                            var timeUntilA = timerA.ElapsedMilliseconds;
                            Console.WriteLine($"{timeUntilA} ms until we received the ack from A.");
                            Console.ReadLine();
                        }
                    }
                    else if (ackConsumer == "B")
                    {
                        Bctr++;
                        if (Bctr == 1000)
                        {
                            timerB.Stop();
                            var timeUntilB = timerB.ElapsedMilliseconds;
                            Console.WriteLine($"{timeUntilB} ms until we received the ack from A.");
                            Console.ReadLine();
                        }
                    }
                    else if (ackConsumer == "C")
                    {
                        Cctr++;
                        if (Cctr == 1000)
                        {
                            timerC.Stop();
                            var timeUntilC = timerC.ElapsedMilliseconds;
                            Console.WriteLine($"{timeUntilC} ms until we received the ack from A.");
                            Console.ReadLine();
                        }
                    }

                    //var ackConsumer = ea.BasicProperties.MessageId;
                    //Console.WriteLine(msg);
                    //if (ackConsumer == "A")
                    //{
                    //    timerA.Stop();
                    //    var timeUntilA = timerA.ElapsedMilliseconds;
                    //    Console.WriteLine($"{timeUntilA} ms until we received the ack from A.");
                    //}
                    //else if (ackConsumer == "B")
                    //{
                    //    timerB.Stop();
                    //    var timeUntilB = timerB.ElapsedMilliseconds;
                    //    Console.WriteLine($"{timeUntilB} ms until we received the ack from B.");
                    //}
                    //else if (ackConsumer == "C")
                    //{
                    //    timerC.Stop();
                    //    var timeUntilC = timerC.ElapsedMilliseconds;
                    //    Console.WriteLine($"{timeUntilC} ms until we received the ack from C.");
                    //}
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

                    var rnd = new Random();
                    var doubleLongitude = rnd.NextDouble();
                    var doubleLatitude = rnd.NextDouble();
                    double latitude = doubleLatitude + rnd.Next(45, 55);
                    double longitude = doubleLongitude + rnd.Next(5, 15);
                    var geolocation = $"{latitude}, {longitude}";

                    Guid unit = Guid.NewGuid();
                    var unitID = unit.ToString();

                    var message = $"{timestamp}/{latitude}/{longitude}/{unitID}";
                    var body = Encoding.UTF8.GetBytes(message);

                    var properties = channel.CreateBasicProperties();
                    properties.Headers = new Dictionary<string, object>();
                    properties.Headers.Add("Sender", myName);
                    properties.MessageId = strGuid;

                    try
                    {

                        Console.WriteLine($"I'm starting to send 1.000 BDs at {DateTime.Now}:{DateTime.Now.Millisecond}");
                        for (int i = 0; i < 1000; i++)
                        {
                            Console.WriteLine($"I: {i}"); //lediglich zu Testzwecken
                            timerA.Start();
                            timerB.Start();
                            timerC.Start();
                            channel.BasicPublish(
                            exchange: "bluedots",
                            routingKey: "",
                            basicProperties: properties,
                            body: body);
                            if (i == 999)
                            {
                                Console.ReadLine();
                            }
                        }

                        //Console.WriteLine($"\nSending a new BlueDot at {DateTime.Now}:{DateTime.Now.Millisecond}.\nID: {unitID}\n");
                        //timerA.Start();
                        //timerB.Start();
                        //timerC.Start();
                        //channel.BasicPublish(
                        //exchange: "bluedots",
                        //routingKey: "",
                        //basicProperties: properties,
                        //body: body);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }

                    // Um die Dokumentation der Messungen zu vereinfachen wurde der Zyklus auf 1 - 3 min verändert (= 60.000 bis 180.000 ms)
                    var cyclic = rnd.Next(60000, 180000);

                    // Blue Dots sollen im Zyklus von 1 sek bis 5 min erstellt werden (= 1.000 bis 300.000 ms)
                    //var cyclic = rnd.Next(1000, 300000);
                    Console.WriteLine(cyclic / 60000 + " minutes and " + (cyclic % 60000) / 1000 + " seconds until the next bluedot.\n");
                    Thread.Sleep(cyclic);
                }
                
            }
        }
    }
}
