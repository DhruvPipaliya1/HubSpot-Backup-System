using Serilog;
using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Receiver
{
    public class HubspotApi
    {
        private static readonly HttpClient httpClient = new HttpClient();


        /// <summary>
        /// Retrieves a CRM record from the HubSpot API for the specified object type and ID.
        /// </summary>
        public static async Task<JsonElement> GetRecordAsync(string accessToken, string objectType, long objectId)
        {
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            var properties = objectType.ToLower() switch
            {
                "contacts" => "firstname,lastname,createdate,lastmodifieddate",
                "companies" => "name,createdate,lastmodifieddate",
                "deals" => "dealname,createdate,lastmodifieddate",
                "tickets" => "subject,createdate,lastmodifieddate",
                _ => "name,createdate,lastmodifieddate"
            };

            var url = $"https://api.hubapi.com/crm/v3/objects/{objectType}/{objectId}?properties={properties}";

            var response = await httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                throw new Exception($"HubSpot API error: {response.StatusCode} for {objectType}/{objectId}");

            var content = await response.Content.ReadAsStringAsync();

            return JsonDocument.Parse(content).RootElement;
        }


        /// <summary>
        /// Asynchronously retrieves the portal name associated with the specified HubSpot access token.
        /// </summary>
        public static async Task<string> GetPortalNameAsync(string accessToken)
        {
            try
            {
                httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken);

                var response = await httpClient.GetAsync("https://api.hubapi.com/account-info/v3/details");

                if (!response.IsSuccessStatusCode)
                {
                    Log.Information($"[HubSpot] Account info API error: {response.StatusCode}");
                    return "unknown";
                }

                var content = await response.Content.ReadAsStringAsync();
                JsonElement json = JsonDocument.Parse(content).RootElement;
                    
                if (json.TryGetProperty("companyName", out JsonElement companyName))
                {
                    string name = companyName.GetString();
                    if (!string.IsNullOrEmpty(name))
                        return name.Trim().Replace(" ", "_");
                }

                if (json.TryGetProperty("portalId", out JsonElement portalId))
                    return portalId.GetInt64().ToString();

                return "unknown";
            }
            catch (Exception ex)
            {
                Log.Information($"[HubSpot] Error in GetPortalNameAsync: {ex.Message}");
                return "unknown";
            }
        }



        /// <summary>
        /// Extracts a display name from a JSON record based on the specified object type.
        /// </summary>
        public static string ExtractFileName(JsonElement record, string objectType)
        {
            try
            {
                if (!record.TryGetProperty("properties", out JsonElement props))
                    return "unknown";

                switch (objectType.ToLower())
                {
                    case "contacts":
                        string firstName = props.TryGetProperty("firstname", out JsonElement fn) ? fn.GetString() ?? "" : "";
                        string lastName = props.TryGetProperty("lastname", out JsonElement ln) ? ln.GetString() ?? "" : "";
                        string fullName = $"{firstName} {lastName}".Trim();
                        return string.IsNullOrEmpty(fullName) ? "unknown" : fullName;

                    case "companies":
                        return props.TryGetProperty("name", out JsonElement companyName)
                            ? companyName.GetString() ?? "unknown"
                            : "unknown";

                    case "deals":
                        return props.TryGetProperty("dealname", out JsonElement dealName)
                            ? dealName.GetString() ?? "unknown"
                            : "unknown";

                    case "tickets":
                        return props.TryGetProperty("subject", out JsonElement subject)
                            ? subject.GetString() ?? "unknown"
                            : "unknown";

                    default:
                        return props.TryGetProperty("name", out JsonElement defaultName)
                            ? defaultName.GetString() ?? "unknown"
                            : "unknown";
                }
            }
            catch (Exception ex)
            {
                Log.Information($"Error in ExtractFileName: {ex.Message}");
                return "unknown";
            }
        }
    }
}
