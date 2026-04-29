using Extractor.Models;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text;

namespace Extractor
{
    public class ExtractorWorker
    {
        private static readonly HttpClient httpClient = new HttpClient();

        /// <summary>
        /// Processes the specified extractor message by retrieving related objects from HubSpot.
        /// </summary>
        public static async Task ProcessExtractor(ExtractorMessage data, string accessToken, string nameKeyword, DateTime? dateFrom, DateTime? dateTo)
        {
            if(data == null)
            {
                Log.Information("Data is not available.");
                return;
            }

            await FetchObjectsFromHubspot(data.Id, data.userId, data.ObjectType, accessToken, nameKeyword, dateFrom, dateTo);
        }
        
        
        /// <summary>
        /// Fetches a list of HubSpot object IDs for the specified object type, applying user-specific search filters if
        /// available.
        /// </summary>
        public static async Task<List<string>> FetchObjectsFromHubspot(int DirId, int UserId, string ObjectType, string accessToken, string nameKeyword, DateTime? dateFrom, DateTime? dateTo)
        {
            List<string> result = new List<string>();

            try
            {
                var service = new HubspotApi(httpClient);

                JObject jsonResult = await service.FetchObjectsAsync(
                    accessToken,
                    ObjectType,
                    nameKeyword,
                    dateFrom,
                    dateTo
                );

                var results = jsonResult["results"];

                if (results != null)
                {
                    foreach (var item in results)
                    {
                        string objectId = item["id"]?.ToString();

                        if (!string.IsNullOrEmpty(objectId))
                        {
                            result.Add(objectId);
                            DbOperation.InsertObjectInDb(UserId, DirId, objectId);

                            Log.Information($"[EXTRACTOR] Inserted ObjectId={objectId} | Type={ObjectType}");
                        }
                    }
                }

                Log.Information($"[EXTRACTOR] Total {result.Count} objects fetched for {ObjectType}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in FetchObjectsFromHubspot");
            }

            return result;
        }
    }
}