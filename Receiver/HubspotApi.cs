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

            var url = $"https://api.hubapi.com/crm/v3/objects/{objectType}/{objectId}?properties=hs_object_id,createdate,lastmodifieddate";

            var response = await httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                throw new Exception($"HubSpot API error: {response.StatusCode} for {objectType}/{objectId}");

            var content = await response.Content.ReadAsStringAsync();

            return JsonDocument.Parse(content).RootElement;
        }
    }
}
