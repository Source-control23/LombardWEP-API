using LombardAPI_NEW.Extensions;
using LombardAPI_NEW.Interfaces;
using LombardAPI_NEW.Models.DTOs;
using LombardAPI_NEW.Models.Enums;
using LombardAPI_NEW.Shared;
using System.Text.Json;

namespace LombardAPI_NEW.Services
{
    public class GALService : IGAL
    {
        private readonly ILogger<GALService> _logger;

        public GALService(ILogger<GALService> logger)
        {
            _logger = logger;
        }

        public async Task<GALSearchResponseDTO> SearchAsync(GALSearchEnum parameter, string value)
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
                    return null;
                }

                // 1. Build PS command (NO ConvertTo-Json here)
                var psCmd = BuildGALSearchCommand(parameter, value);
                _logger.LogInformation("Executing GAL PowerShell command.");
                _logger.LogDebug("PowerShell Command: {Command}", psCmd);

                // 2. Execute PowerShell
                var jsonResult = Powershell.RunPowerShell(psCmd);

                _logger.LogDebug(
                    "PowerShell output length: {Length}, snippet: {Snippet}",
                    jsonResult?.Length ?? 0,
                    SafeSnippet(jsonResult)
                );

                if (string.IsNullOrWhiteSpace(jsonResult))
                {
                    _logger.LogWarning("Empty PowerShell output received.");
                    return null;
                }

                // 3. Deserialize safely
                JsonElement adResult;
                try
                {
                    adResult = JsonSerializer.Deserialize<JsonElement>(jsonResult);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "JSON deserialization failed for GAL search.");
                    throw;
                }

                // Handle multiple results
                JsonElement adUser =
                    adResult.ValueKind == JsonValueKind.Array
                        ? adResult[0]
                        : adResult;

                _logger.LogInformation("AD user record resolved.");

                // 4. Map fields (ICICI aligned)
                var response = new GALSearchResponseDTO
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

                _logger.LogInformation("GAL search completed successfully.");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in GAL search.");
                throw;
            }
        }

        private string BuildGALSearchCommand(GALSearchEnum parameter, string value)
        {
            ///For UAT - Get-ADUser -Filter "SamAccountName -eq 'ilom40106'" -Server 172.30.48.159 -Properties *
            return parameter switch
            {
                GALSearchEnum.EmpId =>
                    $"Get-ADUser -Filter {{ SamAccountName -like \"*{value}*\" }} -Properties *",

                GALSearchEnum.FirstName =>
                    $"Get-ADUser -Filter {{ GivenName -like \"*{value}*\" }} -Properties *",

                GALSearchEnum.LastName =>
                    $"Get-ADUser -Filter {{ Surname -like \"*{value}*\" }} -Properties *",

                GALSearchEnum.Mail =>
                    $"Get-ADUser -Filter {{ Mail -like \"*{value}*\" }} -Properties *",

                GALSearchEnum.DisplayName =>
                    $"Get-ADUser -Filter {{ DisplayName -like \"*{value}*\" }} -Properties *",

                _ => throw new ArgumentException("Invalid GAL search parameter")
            };
        }
        private string SafeSnippet(string input, int length = 200)
        {
            if (string.IsNullOrEmpty(input))
                return "NULL/EMPTY";

            return input.Length <= length
                ? input
                : input.Substring(0, length) + "...";
        }

    }
}
