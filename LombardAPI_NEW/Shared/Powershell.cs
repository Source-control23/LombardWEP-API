using LombardAPI_NEW.DataBase;
using LombardAPI_NEW.Services;
using Microsoft.EntityFrameworkCore;
using System.Management.Automation;
using System.Text.Json;

namespace LombardAPI_NEW.Shared
{
    public class Powershell
    { 
        public static string RunPowerShell(string command)
        {
            using var ps = PowerShell.Create();

            ps.AddScript(@"
        Set-ExecutionPolicy -Scope Process Bypass -Force;
        Import-Module ActiveDirectory -ErrorAction Stop;
    ");

            ps.Invoke();
            ThrowIfErrors(ps);

            ps.Commands.Clear();
            ps.AddScript(command);

            var results = ps.Invoke();

            var errors = ps.Streams.Error.Select(e => e.ToString()).ToList();
            var output = results.Select(r => r.ToString()).ToList();

            if (errors.Any())
            {
                throw new Exception("PowerShell errors: " + string.Join(" | ", errors));
            }
            
            return string.Join(Environment.NewLine, output);
        }
        public static string RunPowerShellCloudConnect(string connectScript, string command)
        {
            using var ps = PowerShell.Create();

            var fullScript = @"
# Fix TEMP path (avoids systemprofile issues)
$env:TEMP = 'C:\Temp'
$env:TMP = 'C:\Temp'
if (!(Test-Path 'C:\Temp')) {
    New-Item -ItemType Directory -Path 'C:\Temp' | Out-Null
}
 
Set-ExecutionPolicy -Scope Process Bypass -Force
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
 
# Import Exchange module
Import-Module ExchangeOnlineManagement -ErrorAction Stop
 
# Connect to Exchange
" + connectScript + @"
 
# Run actual command
" + command;

            ps.AddScript(fullScript);

            var results = ps.Invoke();

            if (ps.HadErrors)
            {
                var errors = ps.Streams.Error.Select(e => e.ToString());
                throw new Exception("PowerShell errors: " + string.Join(" | ", errors));
            }

            return string.Join(Environment.NewLine, results.Select(r => r.ToString()));
        }
        private static void ThrowIfErrors(PowerShell ps)
        {
            if (ps.Streams.Error.Count > 0)
                throw new Exception(string.Join(" | ", ps.Streams.Error.Select(e => e.ToString())));
        }
    }
}