using LombardAPI_NEW.DataBase;
using LombardAPI_NEW.ErrorHandling;
using LombardAPI_NEW.Interfaces;
using LombardAPI_NEW.Models;
using LombardAPI_NEW.Models.DTOs;
using LombardAPI_NEW.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace LombardAPI_NEW.Services
{
    public class GroupMembershipService : IGroupMembership
    {
        private readonly DataBaseContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<GroupMembershipService> _logger;


        public GroupMembershipService(
            DataBaseContext context,
            IEmailService emailService,
            ILogger<GroupMembershipService> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;

        }

        private string SafeSnippet(string input, int length = 200)
        {
            if (string.IsNullOrEmpty(input)) return "NULL/EMPTY";
            return input.Length <= length ? input : input.Substring(0, length) + "...";
        }


        public async Task<AddOrRemoveDLMembershipDTO> GetByDLNameAsync(string dlName)
        {
            using var scope = _logger.BeginScope("GetByDLNameAsync | DLName: {DLName}", dlName);

            try
            {
                _logger.LogInformation("Method started.");

                if (string.IsNullOrWhiteSpace(dlName))
                {
                    _logger.LogWarning("DL name is null or empty.");
                    throw new ArgumentException("DL name cannot be null or empty");
                }

                _logger.LogInformation("Executing Get-ADGroup command.");

                string groupCommand = $"Get-ADGroup -Identity \"{dlName}\" -Properties ManagedBy | ConvertTo-Json -Depth 5";

                _logger.LogDebug("Group PS Command: {Command}", groupCommand);

                var groupResult = Powershell.RunPowerShell(groupCommand);
                _logger.LogInformation($"GroupResult from cmd : {groupResult}");
                groupResult = string.Join("", groupResult.Select(r => r.ToString()));

                _logger.LogDebug(
                    "Group PS Result Length: {Length}, Snippet: {Snippet}",
                    groupResult?.Length ?? 0,
                    SafeSnippet(groupResult)
                );

                if (string.IsNullOrEmpty(groupResult))
                {
                    _logger.LogError("PowerShell returned empty result for Get-ADGroup.");
                    throw new Exception("Group not found");
                }

                JsonElement groupJson;
                try
                {
                    groupJson = JsonSerializer.Deserialize<JsonElement>(groupResult);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to deserialize Get-ADGroup JSON.");
                    throw;
                }

                if (!groupJson.TryGetProperty("ManagedBy", out var managedByProp))
                {
                    _logger.LogWarning("ManagedBy property not found in AD group response.");
                    return new AddOrRemoveDLMembershipDTO
                    {
                        DLName = dlName,
                        DLOwner = "No Owner Assigned"
                    };
                }

                string managedByDn = managedByProp.GetString();

                _logger.LogInformation("ManagedBy DN: {ManagedByDn}", managedByDn);

                if (string.IsNullOrEmpty(managedByDn))
                {
                    _logger.LogInformation("ManagedBy DN is empty.");
                    return new AddOrRemoveDLMembershipDTO
                    {
                        DLName = dlName,
                        DLOwner = "No Owner Assigned"
                    };
                }

                _logger.LogInformation("Executing Get-ADUser command.");

                string ownerCommand =
    $"Get-ADUser -Identity \"{managedByDn}\" -Properties DisplayName,SamAccountName | " +
                    "Select-Object DisplayName,SamAccountName | ConvertTo-Json";

                _logger.LogDebug("Owner PS Command: {Command}", ownerCommand);

                var ownerResult = Powershell.RunPowerShell(ownerCommand);

                _logger.LogDebug(
                    "Owner PS Result Length: {Length}, Snippet: {Snippet}",
                    ownerResult?.Length ?? 0,
                    SafeSnippet(ownerResult)
                );

                if (string.IsNullOrEmpty(ownerResult))
                {
                    _logger.LogError("PowerShell returned empty result for Get-ADUser.");
                    throw new Exception("Owner details not found");
                }

                JsonElement ownerJson;
                try
                {
                    ownerJson = JsonSerializer.Deserialize<JsonElement>(ownerResult);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to deserialize Get-ADUser JSON.");
                    throw;
                }

                string displayName = ownerJson.GetProperty("DisplayName").GetString();
                string samAccountName = ownerJson.GetProperty("SamAccountName").GetString();

                _logger.LogInformation(
                    "DL owner resolved successfully. DisplayName: {DisplayName}, SamAccountName: {Sam}",
                    displayName,
                    samAccountName
                );

                return new AddOrRemoveDLMembershipDTO
                {
                    DLName = dlName,
                    DLOwner = $"{displayName} ({samAccountName})"
                };
            }
            catch (ServiceException)
            {
                _logger.LogWarning("ServiceException thrown, rethrowing.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in GetByDLNameAsync.");
                throw new ServiceException("Unable to fetch DL details.", ex);
            }
        }

        public async Task<AddOrRemoveDLMembershipDTO> GetByDLNameCloudAsync(string dlName)
        {
            using var scope = _logger.BeginScope("GetByDLNameCloudAsync | DLName: {DLName}", dlName);

            try
            {
                _logger.LogInformation("Method started.");

                if (string.IsNullOrWhiteSpace(dlName))
                {
                    _logger.LogWarning("DL name is null or empty.");
                    throw new ArgumentException("DL name cannot be null or empty");
                }

                // 🔹 DEBUG MODE — return fake data, skip PowerShell
                if ("y" == "yy")
                {
                    _logger.LogWarning("DEBUG MODE enabled. Returning mocked DL owner.");
                    return new AddOrRemoveDLMembershipDTO
                    {
                        DLName = dlName,
                        DLOwner = "Debug Owner (ILOM00000)"
                    };
                }

                /* ===============================
                   STEP 1: GET AD GROUP
                ================================*/

                _logger.LogInformation("Executing Get-ADGroup command.");

                string groupCommand =
                    $"Get-ADGroup -Identity \"{dlName}\" -Properties ManagedBy | ConvertTo-Json -Depth 5";

                _logger.LogDebug("Group PS Command: {Command}", groupCommand);

                var groupResult = Powershell.RunPowerShell(groupCommand);

                _logger.LogDebug(
                    "Group PS Result Length: {Length}, Snippet: {Snippet}",
                    groupResult?.Length ?? 0,
                    SafeSnippet(groupResult)
                );

                if (string.IsNullOrEmpty(groupResult))
                {
                    _logger.LogError("Empty response from Get-ADGroup. Possible AD access or DL not found.");
                    throw new Exception("Group not found");
                }

                JsonElement groupJson;
                try
                {
                    groupJson = JsonSerializer.Deserialize<JsonElement>(groupResult);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to deserialize Get-ADGroup JSON.");
                    throw;
                }

                if (!groupJson.TryGetProperty("ManagedBy", out var managedByProp))
                {
                    _logger.LogWarning("ManagedBy property missing in AD group response.");
                    return new AddOrRemoveDLMembershipDTO
                    {
                        DLName = dlName,
                        DLOwner = "No Owner Assigned"
                    };
                }

                string managedByDn = managedByProp.GetString();

                _logger.LogInformation("ManagedBy DN resolved: {ManagedByDn}", managedByDn);

                if (string.IsNullOrEmpty(managedByDn))
                {
                    _logger.LogInformation("ManagedBy DN is empty. DL has no owner.");
                    return new AddOrRemoveDLMembershipDTO
                    {
                        DLName = dlName,
                        DLOwner = "No Owner Assigned"
                    };
                }

                /* ===============================
                   STEP 2: GET OWNER DETAILS
                ================================*/

                _logger.LogInformation("Executing Get-ADUser command.");

                string ownerCommand =
                    $"Get-ADUser -Identity \"{managedByDn}\" -Properties DisplayName,SamAccountName | " +
                    "Select-Object DisplayName,SamAccountName | ConvertTo-Json";

                _logger.LogDebug("Owner PS Command: {Command}", ownerCommand);

                var ownerResult = Powershell.RunPowerShell(ownerCommand);

                _logger.LogDebug(
                    "Owner PS Result Length: {Length}, Snippet: {Snippet}",
                    ownerResult?.Length ?? 0,
                    SafeSnippet(ownerResult)
                );

                if (string.IsNullOrEmpty(ownerResult))
                {
                    _logger.LogError("Empty response from Get-ADUser. Owner DN may be invalid.");
                    throw new Exception("Owner details not found");
                }

                JsonElement ownerJson;
                try
                {
                    ownerJson = JsonSerializer.Deserialize<JsonElement>(ownerResult);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to deserialize Get-ADUser JSON.");
                    throw;
                }

                string displayName = ownerJson.GetProperty("DisplayName").GetString();
                string samAccountName = ownerJson.GetProperty("SamAccountName").GetString();

                _logger.LogInformation(
                    "DL owner fetched successfully. DisplayName: {DisplayName}, SamAccountName: {Sam}",
                    displayName,
                    samAccountName
                );

                return new AddOrRemoveDLMembershipDTO
                {
                    DLName = dlName,
                    DLOwner = $"{displayName} ({samAccountName})"
                };
            }
            catch (ServiceException)
            {
                _logger.LogWarning("ServiceException occurred. Rethrowing.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in GetByDLNameCloudAsync.");
                throw new ServiceException("Unable to fetch DL details.", ex);
            }
        }



        public async Task<bool> AddOrRemoveDLMembershipAsync(AddOrRemoveDLMembershipDTO dto)
        {
            using var scope = _logger.BeginScope(new { dto.DLName, dto.Action });
            try
            {
                if (dto == null)
                    throw new ServiceException("Invalid request payload.");
                _logger.LogInformation("Saving DL request into DB...");
                string savedId = await SaveToDB(dto);

                if (string.IsNullOrEmpty(savedId))
                    throw new ServiceException("Failed to save request in DB.");

                _logger.LogInformation("Sending approval email...");

                string link = $"http://localhost:4200/approval?id={savedId}";

                string body = $@"
  <div style='font-family:Segoe UI,Arial,sans-serif; font-size:14px; color:#333; line-height:1.6;'>
      
      <p>Dear Team,</p>

      <p>
          A new <strong>DL Membership Request</strong> requires your review and approval.
      </p>

      <p>
          Please click the button below to open the approval page:
      </p>

      <p style='margin:20px 0;'>
          <a href='{link}' 
             style='background-color:#0052cc; 
                    color:#fff; 
                    padding:10px 18px; 
                    text-decoration:none; 
                    border-radius:6px;
                    font-weight:600;'>
              Open Approval Page
          </a>
      </p>

      <p>
          If the button above does not work, please copy and paste the link below into your browser:
      </p>

      <p style='word-break:break-all; color:#0052cc;'>
          {link}
      </p>

      <hr style='border:none; border-top:1px solid #e0e0e0; margin:25px 0;' />

      <p style='font-size:12px; color:#777;'>
          This is an automated notification. Please do not reply to this email.
      </p>

  </div>";


                bool emailSent = await _emailService.SendEmailAsync(
                     "",
                    "DL Membership Approval",
                    body
                );

                if (!emailSent)
                    throw new ServiceException("Failed to send approval email.");

                return true;
            }
            catch (ServiceException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected failure in AddOrRemoveDLMembership.");
                throw new ServiceException("Membership update failed.", ex);
            }
        }

        private async Task<string> SaveToDB(AddOrRemoveDLMembershipDTO dto)
        {
            try
            {
                DL dl = null;
                if (dto.Source == "OnPrem")
                {
                     dl = new DL
                    {
                        ApprovalID = Guid.NewGuid().ToString(),
                        Event = "Membership | OnPrem",
                        DLName = dto.DLName,
                        DLOWner = dto.DLOwner,
                        Action = dto.Action,
                        EmpIds = dto.EmpIds,
                        Rights = "",
                        IsLocked = false,
                        CreatedTime = DateTime.UtcNow,
                        ApprovalStatus = "Pending",
                        ApproverEmail = ""
                    };
                }
                else if (dto.Source == "Cloud")
                {
                    dl = new DL
                    {
                        ApprovalID = Guid.NewGuid().ToString(),
                        Event = "Membership | Cloud",
                        DLName = dto.DLName,
                        DLOWner = dto.DLOwner,
                        Action = dto.Action,
                        EmpIds = dto.EmpIds,
                        Rights = "",
                        IsLocked = false,
                        CreatedTime = DateTime.UtcNow,
                        ApprovalStatus = "Pending",
                        ApproverEmail = ""
                    };
                }
               

                await _context.DLDetails.AddAsync(dl);
                await _context.SaveChangesAsync();

                _logger.LogInformation("DL request saved successfully. ApprovalID: {Id}", dl.ApprovalID);
                return dl.ApprovalID;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database save failed for DL: {DLName}", dto.DLName);
                return "";
            }
        }
    }
}
