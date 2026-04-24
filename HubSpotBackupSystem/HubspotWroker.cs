using Newtonsoft.Json.Linq;
using RabbitMQ.Client;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Net.Http.Headers;
using System.Text;

namespace HubSpotBackupSystem
{
    public class HubspotWroker
    {
        private static readonly HttpClient httpClient = new HttpClient();

        private const string ExtractorPath = @"C:\Users\HP\source\repos\HubSpotBackupSystem\Extractor\bin\Debug\net10.0\Extractor.exe";
        private const string ReceiverPath = @"C:\Users\HP\source\repos\HubSpotBackupSystem\Receiver\bin\Debug\net10.0\Receiver.exe";

        private static List<Process> workerProcesses = new List<Process>();


        /// <summary>
        /// Starts multiple worker processes for extractors and receivers required by the server.
        /// </summary>
        public void StartWorker()
        {
            for (int i = 1; i <= 3; i++)
            {
                var extractor = Process.Start(new ProcessStartInfo
                {
                    FileName = ExtractorPath,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                workerProcesses.Add(extractor);
                Log.Information($"[Server] Extractor instance {i} launched.");
            }

            for (int i = 1; i <= 3; i++)
            {
                var receiver = Process.Start(new ProcessStartInfo
                {
                    FileName = ReceiverPath,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                workerProcesses.Add(receiver);
                Log.Information($"[Server] Receiver instance {i} launched.");
            }
        }



        /// <summary>
        /// Processes user data and manages message dispatching to the HubspotReceiver queue until all pending work is
        /// completed.
        /// </summary>
        public async Task SendMessageToQueue()
        {
            await RabbitMQConnection.InitializeAsync();

            int counter = 0;

            List<int> users = DbOperation.GetUsers();
            foreach (int user in users)
            {
                try
                {
                    DbOperation.ResetDirectoryStatusByUserId(user, 1);
                    DbOperation.UpdateWatchStatusForUser(user, 2);
                    await ProcessUser(user);
                    DbOperation.UpdateWatchStatusForUser(user, 5);
                }
                catch (Exception ex)
                {
                    DbOperation.UpdateWatchStatusForUser(user, 3);
                    Log.Information($"Error in StartWorker: {ex.Message}");
                }
            }

            while (true)
            {
                var extractorQueue = await RabbitMQConnection.GetChannel().QueueDeclarePassiveAsync("HubspotExtractor");
                var receiverQueue = await RabbitMQConnection.GetChannel().QueueDeclarePassiveAsync("HubspotReceiver");
                uint extractorCount = extractorQueue.MessageCount;
                uint receiverCount = receiverQueue.MessageCount;
                int pendingDirs = DbOperation.GetPendingDirectoryCount();
                int pendingQueue = DbOperation.GetPendingQueueItemCount();

                Log.Information($"[Server] ExtractorQ={extractorCount} | ReceiverQ={receiverCount} | PendingDirs={pendingDirs} | PendingQueue={pendingQueue}");

                if (extractorCount == 0 && pendingDirs == 0)
                {
                    var statusOneIds = DbOperation.GetQueueEntriesByStatus();

                    if (statusOneIds != null && statusOneIds.Count > 0)
                    {
                        var publisher = new RabbitMQConnection();
                        var statusMessage = new { userId = users.FirstOrDefault(), Ids = statusOneIds };

                        Log.Information($"[QUEUE] Sending {statusOneIds.Count} IDs to HubspotReceiver");

                        await publisher.GetRMQConnection(statusMessage, "HubspotReceiver");
                    }
                }

                bool isEverythingDone = extractorCount == 0 &&
                                        receiverCount == 0 &&
                                        pendingDirs == 0 &&
                                        pendingQueue == 0;

                if (isEverythingDone)
                {
                    counter++;
                    Log.Information($"[Worker] Checking for Process {counter}...");
                    if (counter >= 3)
                    {
                        Log.Information("[Worker] All work confirmed done. Exiting.");
                        break;
                    }
                    await Task.Delay(5000);
                    continue;
                }

                counter = 0;
                await Task.Delay(15000);
            }

            await RabbitMQConnection.DisposeAsync();
        }



        /// <summary>
        /// Synchronizes the user's object directories by adding any missing static or custom objects and sending the
        /// updated directories to the processing queue.
        /// </summary>
        public async Task ProcessUser(int user)
        {
            try
            {
                var dbObjects = DbOperation.GetObjectFromDB(user).Select(x => x.ToLower().Trim()).ToList();

                var staticObjects = new List<string>()
                {
                    "contacts",
                    "companies",
                    "deals",
                    "tickets"
                };

                var customObjects = await GetCustomObject(user);

                var allObjects = staticObjects.Concat(customObjects).Select(x => x.ToLower().Trim()).Distinct();

                foreach (var obj in allObjects)
                {
                    if (!dbObjects.Contains(obj))
                    {
                        Log.Information($"Adding new object {obj} for user {user}");

                        DbOperation.InsertDirectory(user, obj);
                    }
                }

                await SendDirectoriesToQueue(user);
            }
            catch (Exception ex)
            {
                Log.Information("Error in ProceeUser: ", ex.Message);
            }
        }



        /// <summary>
        /// Sends all pending directories for the specified user to the message queue for processing.
        /// </summary>
        public async Task SendDirectoriesToQueue(int user)
        {
            var directories = DbOperation.GetPendingDirectories(user);
            var publisher = new RabbitMQConnection();

            foreach (var dir in directories)
            {
                var message = new
                {
                    userId = user,
                    Id = dir.Id,
                    ObjectType = dir.ObjectType
                };

                Log.Information($"[QUEUE] Sending {message.ObjectType} for user {user}");

                await publisher.GetRMQConnection(message, "HubspotExtractor");

                //DbOperation.UpdateDirectoryStatus(dir.Id, 2);
            }
        }



        /// <summary>
        /// Retrieves the list of custom object schema names available to the specified user from the external API.
        /// </summary>
        public async Task<List<string>> GetCustomObject(int user)
        {
            List<string> result = new List<string>();

            try
            {
                string accessToken = DbOperation.GetAccessToken(user);

                if (string.IsNullOrEmpty(accessToken))
                {
                    Log.Information($"No access token for user {user}");
                    return result;
                }

                httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken);

                var response = await httpClient.GetAsync("https://api.hubapi.com/crm/v3/schemas");

                if (!response.IsSuccessStatusCode)
                {
                    Log.Information($"API Error: {response.StatusCode}");

                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        Log.Information("Token expired. Refresh needed.");
                    }

                    return result;
                }

                var content = await response.Content.ReadAsStringAsync();

                JObject json = JObject.Parse(content);

                var results = json["results"];

                if (results != null)
                {
                    foreach (var item in results)
                    {
                        string name = item["name"]?.ToString();

                        if (!string.IsNullOrEmpty(name))
                        {
                            result.Add(name);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Information($"Error in GetCustomObject: {ex.Message}");
            }

            return result;
        }



        /// <summary>
        /// Terminates all running worker processes managed by the server instance.
        /// </summary>
        public void KillingWorker()
        {
            foreach (var process in workerProcesses)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(true);
                        Log.Information($"[Server] Killed process PID={process.Id}");
                    }
                }
                catch (Exception ex)
                {
                    Log.Information($"[Server] Error killing process: {ex.Message}");
                }
            }
        }
    }
}
