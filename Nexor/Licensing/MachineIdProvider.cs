using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;

namespace Nexor.Licensing
{
    public static class MachineIdProvider
    {
        public static string GetMachineId()
        {
            try
            {
                var macs = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(nic =>
                        nic.OperationalStatus == OperationalStatus.Up &&
                        nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .Select(nic => nic.GetPhysicalAddress().ToString())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct()
                    .OrderBy(s => s)
                    .ToArray();

                var raw = string.Join("-", macs);
                if (string.IsNullOrWhiteSpace(raw))
                    raw = Environment.MachineName;

                using var sha = SHA256.Create();
                var bytes = Encoding.UTF8.GetBytes(raw);
                var hash = sha.ComputeHash(bytes);
                return Convert.ToHexString(hash); // uppercase hex
            }
            catch
            {
                // Last‑resort fallback
                return Environment.MachineName;
            }
        }
    }
}