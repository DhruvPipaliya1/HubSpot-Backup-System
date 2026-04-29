using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text;

namespace HubSpotBackupSystem
{
    public class HubspotApi
    {
        private static readonly HttpClient httpClient = new HttpClient();

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
    }
}
