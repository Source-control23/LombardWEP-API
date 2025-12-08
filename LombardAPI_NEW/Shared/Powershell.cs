using System.Management.Automation;
using System.Text.Json;

namespace LombardAPI_NEW.Shared
{
    public class Powershell
    {
        public static string RunPowerShell(string command)
        {
            using (PowerShell ps = PowerShell.Create())
            {
                ps.AddScript($"{command} | ConvertTo-Json -Depth 10");

                var results = ps.Invoke();

                return string.Join("", results.Select(r => r.ToString()));
            }
        }
    }
}
