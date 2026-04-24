using Serilog;

namespace Extractor
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
            ConfigureLogging("Extractor");
            await RabbitMQConnection.ConsumeRMQMessage();
        }
    }
}