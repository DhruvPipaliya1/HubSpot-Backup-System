using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Receiver
{
    public class FileOperation
    {
        private static readonly string destPath = @"E:\HubspotJson";


        /// <summary>
        /// Saves the specified JSON data to a file named after the provided identifier.
        /// </summary>
        public static string SaveJsonFile(int id, JsonElement data)
        {
            if (!Directory.Exists(destPath))
                Directory.CreateDirectory(destPath);

            string filePath = Path.Combine(destPath, $"{id}.json");

            // Serialize JsonElement directly — preserves exact HubSpot response
            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(filePath, json);

            Log.Information($"[FILE] Saved {filePath}");

            return filePath;
        }
    }
}
