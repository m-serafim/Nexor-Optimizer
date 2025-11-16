using System;
using System.Threading.Tasks;

namespace Nexor.Licensing
{
    public class LicenseService
    {
        private readonly LicenseApiClient _apiClient;

        public LicenseService()
        {
            _apiClient = new LicenseApiClient();
        }

        public async Task<(bool success, string message)> ActivateAsync(string licenseKey)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(licenseKey))
                {
                    return (false, "Please enter a license key");
                }

                string machineId = MachineIdProvider.GetMachineId();
                var response = await _apiClient.ActivateAsync(licenseKey, machineId);

                if (response == null)
                {
                    return (false, "Failed to connect to license server");
                }

                if (response.Success)
                {
                    // Store the license key in a simple text file for now
                    try
                    {
                        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                        string nexorPath = System.IO.Path.Combine(appDataPath, "Nexor");
                        System.IO.Directory.CreateDirectory(nexorPath);
                        string licensePath = System.IO.Path.Combine(nexorPath, "license.dat");
                        await System.IO.File.WriteAllTextAsync(licensePath, licenseKey);
                    }
                    catch { }
                    
                    return (true, response.Message);
                }

                return (false, response.Message);
            }
            catch (Exception ex)
            {
                return (false, $"Activation failed: {ex.Message}");
            }
        }

        public async Task<(bool success, string message)> ValidateAsync()
        {
            try
            {
                // Read the license key from file
                string? licenseKey = null;
                try
                {
                    string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    string licensePath = System.IO.Path.Combine(appDataPath, "Nexor", "license.dat");
                    if (System.IO.File.Exists(licensePath))
                    {
                        licenseKey = await System.IO.File.ReadAllTextAsync(licensePath);
                    }
                }
                catch { }
                
                if (string.IsNullOrWhiteSpace(licenseKey))
                {
                    return (false, "No license key found");
                }

                string machineId = MachineIdProvider.GetMachineId();
                var response = await _apiClient.ValidateAsync(licenseKey, machineId);

                if (response == null)
                {
                    return (false, "Failed to connect to license server");
                }

                return (response.Success, response.Message);
            }
            catch (Exception ex)
            {
                return (false, $"Validation failed: {ex.Message}");
            }
        }
    }
}