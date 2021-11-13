using BDsConsumer.Configurations;
using Microsoft.Extensions.Hosting;
using Npgsql;
using NpgsqlTypes;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BDsConsumer
{
    class BDsConsumer : BackgroundService
    {
        private static string Host = "localhost";
        private static string User = "postgres";
        private static string DBname = "mq";
        private static string Password = "postgres";
        private static string Port = "5432";
        private MyName myName;
        //private List<string> history = new();

        public BDsConsumer(MyName myName)
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
                        using (var command = new NpgsqlCommand ($"SELECT * FROM (SELECT * FROM bluedots_{myName.Value} ORDER BY timestamp DESC LIMIT 100) AS rightorder ORDER BY timestamp ASC", conn))
                        {
                            var reader = command.ExecuteReader();
                            while (reader.Read())
                            {
                                var msg = $"{reader[0]}\t{reader[1]}\t{reader[2]}";
                                var body = Encoding.UTF8.GetBytes(msg);
                                channel.BasicPublish(exchange: "history", routingKey: rK, basicProperties: null, body: body);
                            }
                            reader.Close();
                            channel.BasicPublish(exchange: "history", routingKey: rK, basicProperties: null, body: new byte[0]);
                        }
                    };

                    channel.BasicConsume(queue: helloQueue, autoAck: true, consumer: cons);

                    channel.ExchangeDeclare(exchange: "bluedots", type: ExchangeType.Fanout);
                    var queueName = channel.QueueDeclare().QueueName;
                    channel.QueueBind(
                        queue: queueName,
                        exchange: "bluedots",
                        routingKey: "");

                    Console.WriteLine("[*] Waiting for BlueDots.\n");

                    channel.ExchangeDeclare(exchange: "acks", type: "direct");
                    var consumer = new EventingBasicConsumer(channel);
                    consumer.Received += (model, ea) =>
                    {
                        var body = ea.Body.ToArray();
                        var message = Encoding.UTF8.GetString(body);

                        var time = Convert.ToDateTime(message.Split('/')[0]);
                        var localtime = time.ToLocalTime();

                        var latitude = Convert.ToDouble(message.Split('/')[1]);
                        var longitude = Convert.ToDouble(message.Split('/')[2]);                         
                        Guid unitID = Guid.Parse(message.Split('/')[3]);

                        Console.WriteLine($"\nI received a BlueDot at {DateTime.Now}:{DateTime.Now.Millisecond}.\nID: {unitID}\n");
                        Console.WriteLine("[x] New BlueDot!\nTimestamp: {0}\nGeolocation: {1}, {2}\nUnitID: {3}\n", localtime, latitude, longitude, unitID);

                        using (var command = new NpgsqlCommand($"INSERT INTO bluedots_{myName.Value} (timestamp, geolocation, unitid) VALUES (@t1, @g1, @u1)", conn))
                        {

                            NpgsqlPoint geolocation = new(latitude, longitude);

                            command.Parameters.AddWithValue("t1", time);
                            command.Parameters.AddWithValue("g1", geolocation);
                            command.Parameters.AddWithValue("u1", unitID);
                            command.ExecuteNonQuery();
                        }

                        var senderID = ea.BasicProperties.MessageId;
                        //var nameVal = myName.Value;
                        //var rK = nameVal + senderID;
                        var rK = senderID;
                        var msg = $"The consumer {myName.Value} received and processed the bluedot you sent.\n";
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
