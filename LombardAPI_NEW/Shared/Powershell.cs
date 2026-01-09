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
            using (PowerShell ps = PowerShell.Create())
            {
                ps.AddScript(@"
            Set-ExecutionPolicy -Scope Process Bypass -Force;
            Import-Module ActiveDirectory -ErrorAction Stop;
        ");

                ps.Invoke();
                if (ps.Streams.Error.Count > 0)
                    throw new Exception(string.Join(";", ps.Streams.Error.Select(e => e.ToString())));

                ps.Commands.Clear();

                ps.AddScript(command);

                var results = ps.Invoke();
                if (ps.Streams.Error.Count > 0)
                    throw new Exception(string.Join(";", ps.Streams.Error.Select(e => e.ToString())));

                return string.Join("", results.Select(r => r.ToString()));
            }
        }


    }
}


