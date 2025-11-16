using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace Nexor.Licensing
{
    public class LicensingApiClient
    {
        private readonly HttpClient _http;

        public LicensingApiClient(string baseAddress)
        {
            _http = new HttpClient
            {
                BaseAddress = new Uri(baseAddress)
            };

            _http.DefaultRequestHeaders.Accept.Clear();
            _http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        }

        public record ActivateRequest(string LicenseKey, string MachineId);

        public record ActivateResponse(
            bool Success,
            string Message,
            string Plan,
            string[] Features
        );

        public async Task<string> CreateTestLicenseAsync()
        {
            using var response = await _http.PostAsync("/api/Licenses/create-test", content: null);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);

            if (doc.RootElement.TryGetProperty("licenseKey", out var keyProp))
                return keyProp.GetString() ?? string.Empty;

            throw new InvalidOperationException("create-test response did not contain licenseKey.");
        }

        public async Task<ActivateResponse> ActivateAsync(string licenseKey, string machineId)
        {
            var request = new ActivateRequest(licenseKey, machineId);

            using var response = await _http.PostAsJsonAsync("/api/Licenses/activate", request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ActivateResponse>();
            if (result is null)
                throw new InvalidOperationException("Empty response from /api/Licenses/activate.");

            return result;
        }
    }
}