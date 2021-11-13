using BlobsConsumer.Configurations;
using Microsoft.Extensions.Hosting;
using Npgsql;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BlobsConsumer
{
    class BlobsConsumer : BackgroundService
    {
        private static string Host = "localhost";
        private static string User = "postgres";
        private static string DBname = "mq";
        private static string Password = "postgres";
        private static string Port = "5432";
        private MyName myName;

        public BlobsConsumer(MyName myName)
        {
            this.myName = myName;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
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
                Console.WriteLine("I am Consumer {0}.", myName.Value);

                var factory = new ConnectionFactory();
                factory.UserName = "caren";
                factory.Password = "caren";
                factory.VirtualHost = "/";
                factory.HostName = "caren-pc";
                factory.Port = AmqpTcpEndpoint.UseDefaultPort;

                using (var connection = factory.CreateConnection())
                using (var channel = connection.CreateModel())
                {
                    channel.ExchangeDeclare(exchange: "hello", type: "direct");
                    var helloQueue = channel.QueueDeclare().QueueName;                    
                    channel.QueueBind(queue: helloQueue, exchange: "hello", routingKey: myName.Value);

                    channel.ExchangeDeclare(exchange: "history", type: "direct");

                    var cons = new EventingBasicConsumer(channel);
                    cons.Received += (model, ea) =>
                    {
                        var senderID = ea.BasicProperties.MessageId;
                        var nameVal = myName.Value;
                        var rK = nameVal + senderID;
                        using (var command = new NpgsqlCommand ($"SELECT timestamp FROM (SELECT timestamp FROM blobs_{myName.Value} ORDER BY timestamp DESC LIMIT 100) AS rightorder ORDER BY timestamp ASC", conn))
                        {
                            var reader = command.ExecuteReader();
                            while (reader.Read())
                            {
                                var msg = $"New Blob at {reader[0]}.";
                                var body = Encoding.UTF8.GetBytes(msg);
                                channel.BasicPublish(exchange: "history", routingKey: rK, basicProperties: null, body: body);
                            }
                            reader.Close();
                            channel.BasicPublish(exchange: "history", routingKey: rK, basicProperties: null, body: new byte[0]);
                        }
                    };
                    channel.BasicConsume(queue: helloQueue, autoAck: true, consumer: cons);



                    channel.ExchangeDeclare(exchange: "blobs", type: ExchangeType.Fanout);
                    var queueName = channel.QueueDeclare().QueueName;
                    channel.QueueBind(
                        queue: queueName,
                        exchange: "blobs",
                        routingKey: "");

                    Console.WriteLine("[*] Waiting for Blobs.\n");

                    channel.ExchangeDeclare(exchange: "acks", type: "direct");
                    var consumer = new EventingBasicConsumer(channel);
                    consumer.Received += (model, ea) =>
                    {
                        var body = ea.Body.ToArray();
                        var timestampBytes = body.Take(sizeof(long)).ToArray();
                        var timestampTicks = BitConverter.ToInt64(timestampBytes);
                        var timestamp = new DateTime(timestampTicks, DateTimeKind.Utc);
                        var localtime = timestamp.ToLocalTime();                        
                        var imgBytes = body.Skip(sizeof(long)).ToArray();
                        Console.WriteLine($"\nI received a Blob at {DateTime.Now}:{DateTime.Now.Millisecond}.\n");


                        Console.WriteLine("[x] New Blob at {0}.", localtime);

                        using (var command = new NpgsqlCommand($"INSERT INTO blobs_{myName.Value} (timestamp, images) VALUES (@t1, @i1)", conn))
                        {
                            command.Parameters.AddWithValue("t1", timestamp);
                            command.Parameters.AddWithValue("i1", imgBytes);
                            command.ExecuteNonQuery();
                        }

                        var senderID = ea.BasicProperties.MessageId;
                        var nameVal = myName.Value;
                        //var rK = nameVal + senderID;
                        var rK = senderID;

                        var msg = $"The consumer {nameVal} received and processed the blob you sent.\n";
                        var msgBytes = Encoding.UTF8.GetBytes(msg);
                        var ackProp = channel.CreateBasicProperties();
                        ackProp.MessageId = myName.Value;
                        channel.BasicPublish(exchange: "acks", routingKey: rK, basicProperties: ackProp, body: msgBytes);
                    };

                    while (!stoppingToken.IsCancellationRequested)
                    {
                        channel.BasicConsume(
                        queue: queueName,
                        autoAck: true,
                        consumer: consumer);
                    }
                }
            }
        }
    }
}
