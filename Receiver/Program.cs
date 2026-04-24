using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Receiver;
using Receiver.Models;
using Serilog;
using System.Collections;
using System.Text;
using System.Text.Json;

namespace Receiver
{
    public class Program
    {
        public static void ConfigureLogging(string appName)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.WithProperty("App", appName)
                .Enrich.WithProcessId()
                .WriteTo.Console()
                .WriteTo.File(
                    path: $"E:/logs/{appName}.log",
                    shared: true,
                    flushToDiskInterval: TimeSpan.FromSeconds(1),
                    outputTemplate:
                    "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] [PID:{ProcessId}] [Dir:{Directory}] {Message}{NewLine}{Exception}")
                .CreateLogger();
        }

        public static async Task Main(string[] args)
        {
            ConfigureLogging("Receiver");

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
                        var data = JsonSerializer.Deserialize<ReceiverMessage>(message);

                        Log.Information($"[RECEIVED] userId={data.userId} | IDs=[{string.Join(", ", data.Ids)}]");

                        var queueEntries = DbOperation.GetQueueEntriesByIds(data.Ids);

                        if (queueEntries.Count == 0)
                        {
                            Log.Information("[INFO] No matching entries found with status 1.");
                            await channel.BasicAckAsync(ea.DeliveryTag, false);
                            return;
                        }


                        foreach (QueueItem entry in queueEntries)
                        {
                            string objectType = DbOperation.GetObjectTypeByDirectoryId(entry.DirectoryId);
                            string accessToken = DbOperation.GetAccessToken(data.userId);

                            JsonElement record = await HubspotApi.GetRecordAsync(accessToken, objectType, entry.ObjectId);

                            //Apply filter check 
                            var (nameKeyword, dateFrom, dateTo) = DbOperation.GetSearchFilter(data.userId, objectType);

                            // Check name filter
                            if (!string.IsNullOrEmpty(nameKeyword))
                            {
                                string name = "";

                                if (record.TryGetProperty("properties", out JsonElement props) &&
                                    props.TryGetProperty("name", out JsonElement nameProp))
                                {
                                    name = nameProp.GetString() ?? "";
                                }

                                if (!name.Contains(nameKeyword, StringComparison.OrdinalIgnoreCase))
                                {
                                    Log.Information($"[SKIP] ObjectId={entry.ObjectId} name '{name}' does not match keyword '{nameKeyword}'");
                                    DbOperation.UpdateQueueEntryStatus(entry.Id, 2);
                                    continue;
                                }
                            }

                            // Check date range filter
                            DateTime currentCreated = record.GetProperty("createdAt").GetDateTime();

                            if (dateFrom.HasValue && currentCreated < dateFrom.Value)
                            {
                                Log.Information($"[SKIP] ObjectId={entry.ObjectId} createdAt={currentCreated} is before DateFrom={dateFrom.Value}");
                                DbOperation.UpdateQueueEntryStatus(entry.Id, 2);
                                continue;
                            }

                            if (dateTo.HasValue && currentCreated > dateTo.Value)
                            {
                                Log.Information($"[SKIP] ObjectId={entry.ObjectId} createdAt={currentCreated} is after DateTo={dateTo.Value}");
                                DbOperation.UpdateQueueEntryStatus(entry.Id, 2);
                                continue;
                            }

                            // Passed all filters — proceed with backup
                            DateTime currentModified = record.GetProperty("updatedAt").GetDateTime();
                            DateTime? lastModified = DbOperation.GetLastModifiedByObjectId(entry.ObjectId);

                            if (lastModified.HasValue && currentModified <= lastModified.Value)
                            {
                                Log.Information($"[SKIP] ObjectId={entry.ObjectId} not modified.");
                                continue;
                            }

                            string originalLocation = record.GetProperty("url").GetString();

                            int insertedId = DbOperation.InsertHubspotEntryAndGetId(
                                userId: data.userId,
                                directoryId: entry.DirectoryId,
                                objectId: entry.ObjectId,
                                originalLocation: originalLocation,
                                created: currentCreated,
                                modified: currentModified
                            );

                            if (insertedId <= 0)
                                throw new Exception($"DB insert failed for ObjectId={entry.ObjectId}");

                            string nativeLocation = FileOperation.SaveJsonFile(insertedId, record);
                            DbOperation.UpdateNativeLocation(insertedId, nativeLocation);
                            DbOperation.UpdateQueueEntryStatus(entry.Id, 5);

                            Log.Information($"[DONE] Id={insertedId} | ObjectType={objectType} | File={nativeLocation}");
                        }

                        // All entries done — update status to 5
                        //DbOperation.UpdateWatchStatusForUser(data.userId, 5);

                        await channel.BasicAckAsync(ea.DeliveryTag, false);
                        Log.Information($"[ACK] userId={data.userId} processed successfully.");
                    }
                    catch (Exception ex)
                    {
                        Log.Information($"[ERROR] {ex.Message}");
                        await channel.BasicNackAsync(ea.DeliveryTag, false, requeue: true);
                    }
                };

                await channel.BasicConsumeAsync(
                    queue: "HubspotReceiver",
                    autoAck: false,
                    consumer: consumer
                );

                Log.Information("Waiting for messages...");
                await Task.Delay(Timeout.Infinite);
            }
            catch (Exception ex)
            {
                Log.Information("Error in Receiver: ", ex);
            }
        }
    }
}