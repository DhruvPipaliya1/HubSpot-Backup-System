using RabbitMQ.Client;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace HubSpotBackupSystem
{
    //public class RabbitMQConnection
    //{
    //    /// <summary>
    //    /// Used to upload messages into RabbitMQ Queue.
    //    /// It fetches Directories batch from Db whose watch status is 1.
    //    /// Send those ids to RabbitMQ Queue
    //    /// </summary>
    //    public async Task GetRMQConnection(object message, string queue)
    //    {
    //        var factory = new ConnectionFactory { HostName = "localhost" };

    //        using var connection = await factory.CreateConnectionAsync();
    //        using var channel = await connection.CreateChannelAsync();

    //        await channel.QueueDeclareAsync(queue, true, false, false);

    //        var json = JsonSerializer.Serialize(message);
    //        var body = Encoding.UTF8.GetBytes(json);

    //        await channel.BasicPublishAsync(
    //            exchange: string.Empty,
    //            routingKey: queue,
    //            body: body);
    //    }
    //}


    public class RabbitMQConnection
    {
        private static IConnection connection;
        private static IChannel channel;

        public static IChannel GetChannel()
        {
            return channel;
        }


        /// <summary>
        /// Initializes the connection and declares the required message queues asynchronously.
        /// </summary>
        public static async Task InitializeAsync()
        {
            var factory = new ConnectionFactory { HostName = "localhost" };

            connection = await factory.CreateConnectionAsync();
            channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync("HubspotExtractor", true, false, false);
            await channel.QueueDeclareAsync("HubspotReceiver", true, false, false);

            Log.Information("[RMQ] Connection initialized.");
        }


        /// <summary>
        /// Publishes a message to the specified RabbitMQ queue asynchronously.
        /// </summary>
        public async Task GetRMQConnection(object message, string queue)
        {
            var json = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(json);

            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: queue,
                body: body
            );
        }


        /// <summary>
        /// Asynchronously releases resources used by the connection and channel.
        /// </summary>
        public static async Task DisposeAsync()
        {
            if (channel != null) await channel.DisposeAsync();
            if (connection != null) await connection.DisposeAsync();

            Log.Information("[RMQ] Connection disposed.");
        }
    }
}
