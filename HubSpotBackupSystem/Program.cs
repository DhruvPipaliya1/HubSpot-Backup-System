using HubSpotBackupSystem;
using Serilog;
using System.Diagnostics;

namespace HuSpotBackupSystem
{

    
    class Program
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
            ConfigureLogging("Server");
            DbOperation.ResetWatchStatusForUser();

            HubspotWroker hw = new HubspotWroker();

            hw.StartWorker();

            Log.Information("[Server] Starting HubSpot worker loop...");

            try
            {
                await hw.SendMessageToQueue();
            }
            catch (Exception ex) 
            {
                Log.Information("Error in SendMessageToQueue: " + ex.Message);
            }
            finally
            {
                Log.Information("[Server] Stopping all workers...");

                hw.KillingWorker();
            }
        }

        
    }
}