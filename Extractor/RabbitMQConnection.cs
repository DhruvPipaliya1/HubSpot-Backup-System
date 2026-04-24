using Extractor.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Extractor
{
    public class RabbitMQConnection
    {

        /// <summary>
        /// Consumes messages from the RabbitMQ queue named "HubspotExtractor" and processes each message
        /// asynchronously.
        /// </summary>
        public static async Task ConsumeRMQMessage()
        {
            try
            {
                var factory = new ConnectionFactory()
                {
                    HostName = "localhost"
                };

                var connection = await factory.CreateConnectionAsync();
                var channel = await connection.CreateChannelAsync();

                await channel.BasicQosAsync(0, 5, false);

                var consumer = new AsyncEventingBasicConsumer(channel);

                consumer.ReceivedAsync += async (sender, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);

                    try
                    {
                        var data = JsonSerializer.Deserialize<ExtractorMessage>(message);

                        Log.Information($"[RECEIVED] user={data.userId}, dir={data.Id}, object={data.ObjectType}");

                        DbOperation.UpdateDirectoryStatus(data.Id, 2);
                        Log.Information("Update DirectoryStatus in Folderbackup to 2");

                        await ExtractorWorker.ProcessExtractor(data);

                        DbOperation.UpdateDirectoryStatus(data.Id, 5);
                        Log.Information("Update DirectoryStatus in Folderbackup to 5");

                        await channel.BasicAckAsync(ea.DeliveryTag, false);

                        Log.Information($"[ACK] Processed directory {data.Id}");
                    }
                    catch (Exception ex)
                    {
                        //DbOperation.UpdateDirectoryStatus(data.Id, 3);
                        //Log.Information("Update DirectoryStatus in Folderbackup to 2");
                        Log.Information($"[ERROR] {ex.Message}");

                    }
                };

                await channel.BasicConsumeAsync(
                    queue: "HubspotExtractor",
                    autoAck: false, 
                    consumer: consumer
                );

                Log.Information("Extractor Worker Started. Waiting for messages...");

                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Log.Information($"Error during RabbitMQ Connection: {ex.Message}");
            }
        }
    }
}
