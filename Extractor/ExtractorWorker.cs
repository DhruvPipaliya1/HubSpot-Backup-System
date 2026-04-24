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
        public static async Task ProcessExtractor(ExtractorMessage data)
        {
            if(data == null)
            {
                Log.Information("Data is not available.");
                return;
            }

            await FetchObjectsFromHubspot(data.Id, data.userId, data.ObjectType);
        }

        /*public static async Task<List<string>> FetchObjectsFromHubspot(int DirId, int UserId, string ObjectType)
        {
            List<string> result = new List<string>();

            try
            {
                string accessToken = DbOperation.GetAccessToken(UserId);

                if (string.IsNullOrEmpty(accessToken))
                {
                    Log.Information($"No access token for user {UserId}");
                    return result;
                }

                httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken);

                var response = await httpClient.GetAsync($"https://api.hubapi.com/crm/v3/objects/{ObjectType}");

                if (!response.IsSuccessStatusCode)
                {
                    Log.Information($"API Error: {response.StatusCode}");
                    return result;
                }

                var content = await response.Content.ReadAsStringAsync();

                JObject json = JObject.Parse(content);

                var results = json["results"];

                if (results != null)
                {
                    foreach (var item in results)
                    {
                        string objectId = item["id"]?.ToString();

                        if (!string.IsNullOrEmpty(objectId))
                        {
                            result.Add(objectId);

                            DbOperation.InsertObjectInDb(UserId, DirId, objectId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Information($"Error: {ex.Message}");
            }

            return result;
        }*/


        /// <summary>
        /// Fetches a list of HubSpot object IDs for the specified object type, applying user-specific search filters if
        /// available.
        /// </summary>
        public static async Task<List<string>> FetchObjectsFromHubspot(int DirId, int UserId, string ObjectType)
        {
            List<string> result = new List<string>();

            try
            {
                string accessToken = DbOperation.GetAccessToken(UserId);

                if (string.IsNullOrEmpty(accessToken))
                {
                    Log.Information($"No access token for user {UserId}");
                    return result;
                }

                httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken);

                // Fetch filters from DB for this user + objectType
                var (nameKeyword, dateFrom, dateTo) = DbOperation.GetSearchFilter(UserId, ObjectType);

                // Build HubSpot search filters
                var filterGroups = new List<object>();
                var filters = new List<object>();

                if (!string.IsNullOrEmpty(nameKeyword))
                {
                    filters.Add(new
                    {
                        propertyName = "name",
                        @operator = "CONTAINS_TOKEN",
                        value = nameKeyword
                    });
                }

                if (dateFrom.HasValue)
                {
                    filters.Add(new
                    {
                        propertyName = "createdate",
                        @operator = "GTE",
                        value = new DateTimeOffset(dateFrom.Value).ToUnixTimeMilliseconds().ToString()
                    });
                }

                if (dateTo.HasValue)
                {
                    filters.Add(new
                    {
                        propertyName = "createdate",
                        @operator = "LTE",
                        value = new DateTimeOffset(dateTo.Value).ToUnixTimeMilliseconds().ToString()
                    });
                }

                string content;

                // Use Search API if filters exist, otherwise use basic list API
                if (filters.Count > 0)
                {
                    filterGroups.Add(new { filters = filters });

                    var searchBody = new
                    {
                        filterGroups = filterGroups,
                        properties = new[] { "id", "name", "createdate" },
                        limit = 100
                    };

                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(searchBody);
                    var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

                    var searchResponse = await httpClient.PostAsync(
                        $"https://api.hubapi.com/crm/v3/objects/{ObjectType}/search",
                        httpContent
                    );

                    if (!searchResponse.IsSuccessStatusCode)
                    {
                        Log.Information($"Search API Error: {searchResponse.StatusCode}");
                        return result;
                    }

                    content = await searchResponse.Content.ReadAsStringAsync();
                }
                else
                {
                    // No filters — use basic GET
                    var basicResponse = await httpClient.GetAsync(
                        $"https://api.hubapi.com/crm/v3/objects/{ObjectType}"
                    );

                    if (!basicResponse.IsSuccessStatusCode)
                    {
                        Log.Information($"API Error: {basicResponse.StatusCode}");
                        return result;
                    }

                    content = await basicResponse.Content.ReadAsStringAsync();
                }

                JObject jsonResult = JObject.Parse(content);
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
                Log.Information($"Error: {ex.Message}");
            }

            return result;
        }
    }
}