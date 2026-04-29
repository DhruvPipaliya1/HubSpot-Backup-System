using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text;

namespace Extractor
{
    public class HubspotApi
    {
        private readonly HttpClient _httpClient;

        public HubspotApi(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<JObject> FetchObjectsAsync(
            string accessToken,
            string objectType,
            string nameKeyword,
            DateTime? dateFrom,
            DateTime? dateTo)
        {
            if (string.IsNullOrEmpty(accessToken))
                throw new Exception("Access token is null");

            var request = new HttpRequestMessage();
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var filters = BuildFilters(nameKeyword, dateFrom, dateTo);

            if (filters.Count > 0)
            {
                var body = new
                {
                    filterGroups = new[] { new { filters } },
                    properties = new[] { "id", "name", "createdate" },
                    limit = 100
                };

                var json = Newtonsoft.Json.JsonConvert.SerializeObject(body);
                request.Method = HttpMethod.Post;
                request.RequestUri = new Uri($"https://api.hubapi.com/crm/v3/objects/{objectType}/search");
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }
            else
            {
                request.Method = HttpMethod.Get;
                request.RequestUri = new Uri($"https://api.hubapi.com/crm/v3/objects/{objectType}");
            }

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
                throw new Exception($"HubSpot API error: {response.StatusCode}");

            var content = await response.Content.ReadAsStringAsync();
            return JObject.Parse(content);
        }

        private List<object> BuildFilters(string nameKeyword, DateTime? dateFrom, DateTime? dateTo)
        {
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

            return filters;
        }
    }
}
