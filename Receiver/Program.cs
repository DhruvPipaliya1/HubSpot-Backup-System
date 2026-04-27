using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Receiver;
using Receiver.Models;
using Serilog;
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
                            Log.Information("[INFO] No matching entries found.");
                            await channel.BasicAckAsync(ea.DeliveryTag, false);
                            return;
                        }

                        // Fetch once before the loop — no repeated API calls
                        string accessToken = DbOperation.GetAccessToken(data.userId);
                        string portalName = await HubspotApi.GetPortalNameAsync(accessToken);

                        foreach (QueueItem entry in queueEntries)
                        {
                            // Mark in-progress
                            DbOperation.UpdateQueueEntryStatus(entry.Id, 2);

                            string objectType = DbOperation.GetObjectTypeByDirectoryId(entry.DirectoryId);

                            JsonElement record = await HubspotApi.GetRecordAsync(accessToken, objectType, entry.ObjectId);

                            // ── Filter 1: Name keyword ────────────────────────────────────────────
                            var (nameKeyword, dateFrom, dateTo) = DbOperation.GetSearchFilter(data.userId, objectType);

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
                                    DbOperation.UpdateQueueEntryStatus(entry.Id, 3);
                                    continue;
                                }
                            }

                            // ── Filter 2: Date range ──────────────────────────────────────────────
                            DateTime currentCreated = record.GetProperty("createdAt").GetDateTime();

                            if (dateFrom.HasValue && currentCreated < dateFrom.Value)
                            {
                                Log.Information($"[SKIP] ObjectId={entry.ObjectId} createdAt={currentCreated} is before DateFrom={dateFrom.Value}");
                                DbOperation.UpdateQueueEntryStatus(entry.Id, 3);
                                continue;
                            }

                            if (dateTo.HasValue && currentCreated > dateTo.Value)
                            {
                                Log.Information($"[SKIP] ObjectId={entry.ObjectId} createdAt={currentCreated} is after DateTo={dateTo.Value}");
                                DbOperation.UpdateQueueEntryStatus(entry.Id, 3);
                                continue;
                            }

                            // ── Filter 3: Modified check ──────────────────────────────────────────
                            DateTime currentModified = record.GetProperty("updatedAt").GetDateTime();
                            DateTime? lastModified = DbOperation.GetLastModifiedByObjectId(entry.ObjectId);

                            if (lastModified.HasValue && currentModified <= lastModified.Value)
                            {
                                Log.Information($"[SKIP] ObjectId={entry.ObjectId} not modified. DB={lastModified.Value} | HubSpot={currentModified}");
                                DbOperation.UpdateQueueEntryStatus(entry.Id, 3);
                                continue;
                            }

                            // ── Passed all filters — proceed with backup ──────────────────────────
                            string originalLocation = $@"hubspot\{portalName}\{objectType}\{entry.ObjectId}";
                            string hubspotUrl = record.GetProperty("url").GetString();

                            int insertedId = DbOperation.InsertHubspotEntryAndGetId(
                                userId: data.userId,
                                directoryId: entry.DirectoryId,
                                objectId: entry.ObjectId,
                                originalLocation: originalLocation,
                                hubSpotUrl: hubspotUrl,
                                created: currentCreated,
                                modified: currentModified
                            );

                            if (insertedId <= 0)
                                throw new Exception($"DB insert failed for ObjectId={entry.ObjectId}");

                            DbOperation.UpdateCopyStatus(insertedId, 2);
                            Log.Information("CopyStatus updated to 2");

                            try
                            {
                                string nativeLocation = FileOperation.SaveJsonFile(insertedId, record);
                                DbOperation.UpdateNativeLocation(insertedId, nativeLocation);

                                // ── FileName and FileSize ─────────────────────────────────────────
                                string fileName = HubspotApi.ExtractFileName(record, objectType);
                                long fileSize = FileOperation.GetFileSize(nativeLocation);

                                DbOperation.UpdateFileNameAndSize(insertedId, fileName, fileSize);

                                DbOperation.UpdateCopyStatus(insertedId, 5);
                                Log.Information("CopyStatus updated to 5");
                            }
                            catch (Exception e)
                            {
                                DbOperation.UpdateCopyStatus(insertedId, 3);
                                Log.Information("Error during SaveJsonFile: " + e.Message);
                                Log.Information("CopyStatus updated to 3");
                            }

                            DbOperation.UpdateQueueEntryStatus(entry.Id, 5);

                            Log.Information($"[DONE] Id={insertedId} | ObjectType={objectType} | Path={originalLocation} | URL={hubspotUrl}");
                        }

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
                Log.Information("Error in Receiver: " + ex.Message);
            }
        }
    }
}