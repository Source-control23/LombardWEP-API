using LombardAPI_NEW.Extensions;
using LombardAPI_NEW.Interfaces;
using LombardAPI_NEW.Models;
using LombardAPI_NEW.Models.DTOs;
using LombardAPI_NEW.Models.Enums;
using LombardAPI_NEW.Shared;
using Microsoft.Extensions.Options;
using System.Text;
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
                
                var jsonResult = string.Empty;
                for (int i = 0; i < psCmd.Count; i++)
                {
                    _logger.LogInformation("Executing GAL PowerShell command. : " + psCmd[i].ToString());
                    jsonResult = Powershell.RunPowerShell(psCmd[i]);
                    if(!string.IsNullOrWhiteSpace(jsonResult))
                    {
                        break; // stop at first successful result
                    }
                    _logger.LogDebug("PowerShell Command [{Index}]: {Command}", i, psCmd[i]);
                }
                

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


        private static GALSearchResponseDTO MapUser(JsonElement u)
        {
            return new GALSearchResponseDTO
            {
                // Basic
                FirstName = u.GetPropertyOrDefault("GivenName"),
                LastName = u.GetPropertyOrDefault("Surname"),
                Display = u.GetPropertyOrDefault("DisplayName"),
                Initials = u.GetPropertyOrDefault("Initials"),
                KnownAs = u.GetPropertyOrDefault("Name"),
                Grade = u.GetPropertyOrDefault("EmployeeType"),
                WorkStationID = u.GetPropertyOrDefault("Office"),

                // Employment
                EmpNo = u.GetPropertyOrDefault("EmployeeID"),
                Designation = u.GetPropertyOrDefault("Title"),
                Department = u.GetPropertyOrDefault("Department"),
                Company = u.GetPropertyOrDefault("Company"),
                Location = u.GetPropertyOrDefault("City"),
                Manager = ExtractCommonName(u.GetPropertyOrDefault("Manager")),
                Group = u.GetPropertyOrDefault("personalPager"),
                MainGroup = u.GetPropertyOrDefault("personalPager"),
                CostCenter = u.GetPropertyOrDefault("extensionName"),
                Region = u.GetPropertyOrDefault("o"),

                // Contact
                EmailAddress = u.GetPropertyOrDefault("mail"),
                Mobile = u.GetPropertyOrDefault("MobilePhone"),
                DirectPhone = u.GetPropertyOrDefault("TelephoneNumber"),
                BoardNo = u.GetPropertyOrDefault("homePhone"),
                Fax = u.GetPropertyOrDefault("Fax"),
                Business = u.GetPropertyOrDefault("IpPhone"),
                Business2 = u.GetPropertyOrDefault("OtherTelephone"),
                CountryCode = u.GetPropertyOrDefault("CountryCode"),
                CityCode = u.GetPropertyOrDefault("HomeMDB"),

                // Address
                AddressLocation = u.GetPropertyOrDefault("l"),
                Address = u.GetPropertyOrDefault("StreetAddress"),
                State = u.GetPropertyOrDefault("State"),
                ZipCode = u.GetPropertyOrDefault("PostalCode"),
                Country = u.GetPropertyOrDefault("Country"),

                // Reports
                DirectReports = u.GetPropertyOrDefault("DirectReports"),
                Assisted = u.GetPropertyOrDefault("Assistant")
            };
        }

        private static string? ExtractCommonName(string? distinguishedName)
        {
            if (string.IsNullOrWhiteSpace(distinguishedName))
                return null;

            var match = System.Text.RegularExpressions.Regex.Match(
                distinguishedName,
                @"CN=([^,]+)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            return match.Success ? match.Groups[1].Value : distinguishedName;
        }

        private List<string> BuildGALSearchCommand(GALSearchEnum parameter, string value)
        {
            List<string> result = new List<string>();

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

            if (_cmd.GAL.SearchBaseOUs == null || !_cmd.GAL.SearchBaseOUs.Any())
                throw new InvalidOperationException("No GAL SearchBaseOUs configured.");

            var baseCommand = template
                .Replace("| ConvertTo-Json", "", StringComparison.OrdinalIgnoreCase)
                .Replace("{Value}", value);

            foreach (var ou in _cmd.GAL.SearchBaseOUs)
            {
                var command = $"{baseCommand} -SearchBase \"{ou}\" | Select-Object * | ConvertTo-Json -Depth 4";
                result.Add(command);
            }

            return result;
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
