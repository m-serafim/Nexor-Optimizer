using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Nexor.Licensing
{
    public class LicenseApiClient
    {
        private readonly HttpClient _http;

        // For now this is your local API. When you deploy to the cloud, change this URL.
        private const string BaseUrl = "https://localhost:7113";

        public LicenseApiClient()
        {
            _http = new HttpClient
            {
                BaseAddress = new Uri(BaseUrl)
            };
        }

        public class LicenseResponse
        {
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
            public string? Plan { get; set; }
            public string[]? Features { get; set; }
        }

        public async Task<LicenseResponse?> ActivateAsync(string licenseKey, string machineId)
        {
            var request = new
            {
                licenseKey,
                machineId
            };

            var response = await _http.PostAsJsonAsync("/api/licenses/activate", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<LicenseResponse>();
        }

        public async Task<LicenseResponse?> ValidateAsync(string licenseKey, string machineId)
        {
            var request = new
            {
                licenseKey,
                machineId
            };

            var response = await _http.PostAsJsonAsync("/api/licenses/validate", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<LicenseResponse>();
        }
    }
}