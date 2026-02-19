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

        private static void ThrowIfErrors(PowerShell ps)
        {
            if (ps.Streams.Error.Count > 0)
                throw new Exception(string.Join(" | ", ps.Streams.Error.Select(e => e.ToString())));
        }


    }
}


