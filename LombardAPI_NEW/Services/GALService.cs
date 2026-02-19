using LombardAPI_NEW.Extensions;
using LombardAPI_NEW.Interfaces;
using LombardAPI_NEW.Models;
using LombardAPI_NEW.Models.DTOs;
using LombardAPI_NEW.Models.Enums;
using LombardAPI_NEW.Shared;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace LombardAPI_NEW.Services
{
    public class GALService : IGAL
    {
        private readonly ILogger<GALService> _logger;
        private readonly PowerShellCommandSettings _cmd;

        public GALService(ILogger<GALService> logger, IOptions<PowerShellCommandSettings> options)
        {
            _logger = logger;
            _cmd = options.Value;
        }

//        public async Task<GALSearchResponseDTO> SearchAsync(GALSearchEnum parameter, string value)
//        {
//            using var scope = _logger.BeginScope(
//                "GALSearch | Parameter: {Parameter}, Value: {Value}",
//                parameter, value);

//            try
//            {
//                _logger.LogInformation("GAL search started.");

//                if (string.IsNullOrWhiteSpace(value))
//                {
//                    _logger.LogWarning("Search value is null or empty.");
//                    return null;
//                }

//                // 1. Build PS command (NO ConvertTo-Json here)
//                var psCmd = BuildGALSearchCommand(parameter, value);
//                _logger.LogInformation("Executing GAL PowerShell command.");
//                _logger.LogDebug("PowerShell Command: {Command}", psCmd);

//                // 2. Execute PowerShell
//                var jsonResult = Powershell.RunPowerShell(psCmd);

//                _logger.LogDebug(
//                    "PowerShell output length: {Length}, snippet: {Snippet}",
//                    jsonResult?.Length ?? 0,
//                    SafeSnippet(jsonResult)
//                );

//                if (string.IsNullOrWhiteSpace(jsonResult))
//                {
//                    _logger.LogWarning("Empty PowerShell output received.");
//                    return null;
//                }

//                // 3. Deserialize safely (ensure PS output is JSON)
////                if (!LooksLikeJson(jsonResult))
////                {
////                    _logger.LogError(
////    "PowerShell output is not JSON. Full output: {Output}",
////    jsonResult
////);
////                    return null;
////                }

//                JsonElement adResult;
//                try
//                {
//                    adResult = JsonSerializer.Deserialize<JsonElement>(jsonResult);
//                }
//                catch (Exception ex)
//                {
//                    _logger.LogError(ex, "JSON deserialization failed for GAL search.");
//                    return null;
//                }
//                if (adResult.ValueKind == JsonValueKind.Array && adResult.GetArrayLength() == 0)
//                {
//                    _logger.LogWarning("GAL search returned empty array.");
//                    return null;
//                }
//                // Handle multiple results
//                JsonElement adUser =
//                    adResult.ValueKind == JsonValueKind.Array
//                        ? adResult[0]
//                        : adResult;

//                _logger.LogInformation("AD user record resolved.");

//                // 4. Map fields (ICICI aligned)
//                var response = new GALSearchResponseDTO
//                {
//                    FirstName = adUser.GetPropertyOrDefault("GivenName"),
//                    LastName = adUser.GetPropertyOrDefault("Surname"),
//                    Display = adUser.GetPropertyOrDefault("DisplayName"),
//                    EmpNo = adUser.GetPropertyOrDefault("EmployeeID"),
//                    EmailAddress = adUser.GetPropertyOrDefault("Mail"),
//                    Mobile = adUser.GetPropertyOrDefault("MobilePhone"),
//                    Department = adUser.GetPropertyOrDefault("Department"),
//                    Designation = adUser.GetPropertyOrDefault("Title"),
//                    Manager = adUser.GetPropertyOrDefault("Manager"),
//                    Location = adUser.GetPropertyOrDefault("City"),
//                    Company = adUser.GetPropertyOrDefault("Company"),
//                    Country = adUser.GetPropertyOrDefault("Country")
//                };

//                _logger.LogInformation("GAL search completed successfully.");
//                return response;
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Unhandled exception in GAL search.");
//                throw;
//            }
//        }

        public async Task<List<GALSearchResponseDTO>> SearchAsync(GALSearchEnum parameter, string value)
        {
            using var scope = _logger.BeginScope(
                "GALSearch | Parameter: {Parameter}, Value: {Value}",
                parameter, value);

            try
            {
                _logger.LogInformation("GAL search started.");

                if (string.IsNullOrWhiteSpace(value))
                {
                    _logger.LogWarning("Search value is null or empty.");
                    return new List<GALSearchResponseDTO>();
                }

                var psCmd = BuildGALSearchCommand(parameter, value);
                _logger.LogInformation("Executing GAL PowerShell command.");

                var jsonResult = Powershell.RunPowerShell(psCmd);

                if (string.IsNullOrWhiteSpace(jsonResult))
                {
                    _logger.LogWarning("Empty PowerShell output received.");
                    return new List<GALSearchResponseDTO>();
                }

                JsonElement adResult;

                try
                {
                    adResult = JsonSerializer.Deserialize<JsonElement>(jsonResult);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "JSON deserialization failed for GAL search.");
                    return new List<GALSearchResponseDTO>();
                }

                var results = new List<GALSearchResponseDTO>();

                if (adResult.ValueKind == JsonValueKind.Array)
                {
                    foreach (var user in adResult.EnumerateArray())
                    {
                        results.Add(MapUser(user));
                    }
                }
                else if (adResult.ValueKind == JsonValueKind.Object)
                {
                    results.Add(MapUser(adResult));
                }

                _logger.LogInformation("GAL search completed. Records: {Count}", results.Count);
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in GAL search.");
                throw;
            }
        }

        private static GALSearchResponseDTO MapUser(JsonElement adUser)
        {
            return new GALSearchResponseDTO
            {
                FirstName = adUser.GetPropertyOrDefault("GivenName"),
                LastName = adUser.GetPropertyOrDefault("Surname"),
                Display = adUser.GetPropertyOrDefault("DisplayName"),
                EmpNo = adUser.GetPropertyOrDefault("EmployeeID"),
                EmailAddress = adUser.GetPropertyOrDefault("Mail"),
                Mobile = adUser.GetPropertyOrDefault("MobilePhone"),
                Department = adUser.GetPropertyOrDefault("Department"),
                Designation = adUser.GetPropertyOrDefault("Title"),
                Manager = adUser.GetPropertyOrDefault("Manager"),
                Location = adUser.GetPropertyOrDefault("City"),
                Company = adUser.GetPropertyOrDefault("Company"),
                Country = adUser.GetPropertyOrDefault("Country")
            };
        }

        private string BuildGALSearchCommand(GALSearchEnum parameter, string value)
        {
            string template = parameter switch
            {
                GALSearchEnum.EmpId => _cmd.GAL.EmpId,
                GALSearchEnum.FirstName => _cmd.GAL.FirstName,
                GALSearchEnum.LastName => _cmd.GAL.LastName,
                GALSearchEnum.Mail => _cmd.GAL.Mail,
                GALSearchEnum.DisplayName => _cmd.GAL.DisplayName,
                _ => throw new ArgumentException("Invalid GAL search parameter")
            };

            if (string.IsNullOrWhiteSpace(template))
                throw new InvalidOperationException($"GAL PowerShell command not configured for {parameter}");

            return template.Replace("{Value}", value);
        }

        private string SafeSnippet(string input, int length = 200)
        {
            if (string.IsNullOrEmpty(input))
                return "NULL/EMPTY";

            return input.Length <= length
                ? input
                : input.Substring(0, length) + "...";
        }

        private bool LooksLikeJson(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            var trimmed = input.TrimStart();
            return trimmed.StartsWith("{") || trimmed.StartsWith("[");
        }

    }
}
