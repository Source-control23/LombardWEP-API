using LombardAPI_NEW.DataBase;
using LombardAPI_NEW.ErrorHandling;
using LombardAPI_NEW.Interfaces;
using LombardAPI_NEW.Models;
using LombardAPI_NEW.Models.DTOs;
using LombardAPI_NEW.Shared;
using Microsoft.CodeAnalysis;
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
        private readonly PowerShellCommandSettings _cmd;
        private readonly CommonService _commonService;
        private readonly IMembershipNotificationService _notificationService;
        private readonly EmailRecipientResolver _recipientResolver;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public GroupMembershipService(CommonService commonService,
            DataBaseContext context,
            IEmailService emailService,
            ILogger<GroupMembershipService> logger, IOptions<PowerShellCommandSettings> options,
            IMembershipNotificationService notificationService,
            EmailRecipientResolver recipientResolver,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
            _cmd = options.Value;
            _commonService = commonService;
            _notificationService = notificationService;
            _recipientResolver = recipientResolver;
            _httpContextAccessor = httpContextAccessor;
        }

        public string RunExchangeCloudCommand(string command)
        {
            using var scope = _logger.BeginScope("ExchangeCloud | Command: {Cmd}", command);

            try
            {
                string connectScript = _cmd.Membership.Cloud.ConnectScript;

                if (string.IsNullOrWhiteSpace(connectScript))
                    throw new Exception("CONFIG ERROR: Exchange connect script missing.");

                _logger.LogInformation("STEP 1 → Connecting to Exchange Online");

                var connectResult = Powershell.RunPowerShellCloudConnect(connectScript,command);

                //if (string.IsNullOrWhiteSpace(connectResult))
                //    _logger.LogWarning("Exchange connect returned empty output (normal behavior).");

                //_logger.LogInformation("STEP 2 → Running Exchange command");

                //var result = Powershell.RunPowerShell(command);

                //if (string.IsNullOrWhiteSpace(result))
                //    throw new Exception("Exchange command returned empty result. Possible reasons: DL not found or permission missing.");

                //_logger.LogInformation("STEP 3 → Exchange command executed successfully");

                return connectResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exchange execution failed.");

                throw new ServiceException(
                    "Exchange Cloud execution failed. Possible causes: Certificate issue, App permission missing, or Exchange role not assigned.",
                    ex);
            }
        }

        public Task<AddOrRemoveDLMembershipDTO> GetByDLNameCloudAsync(string dlName)
        {
            using var scope = _logger.BeginScope("Cloud DL Lookup | {DL}", dlName);

            try
            {
                if (string.IsNullOrWhiteSpace(dlName))
                    throw new ServiceException("DL name cannot be empty.");

                _logger.LogInformation("STEP 1 → Preparing PowerShell command");

                string command = _cmd.Membership.Cloud.GetDLWithOwner
                    .Replace("{dlName}", dlName);

                _logger.LogDebug("Exchange Command: {Command}", command);

                _logger.LogInformation("STEP 2 → Executing Exchange lookup");

                var result = RunExchangeCloudCommand(command);

                _logger.LogInformation("STEP 3 → Parsing Exchange result" + result.ToString());

                var json = JsonSerializer.Deserialize<JsonElement>(result);
                _logger.LogInformation("STEP 4 → Parsing result as json" + json.ToString());
                string ownerName = json.GetProperty("OwnerName").ValueKind switch
                {
                    JsonValueKind.Array => string.Join(", ",
                        json.GetProperty("OwnerName").EnumerateArray().Select(x => x.GetString())),
                    _ => json.GetProperty("OwnerName").GetString()
                };
                _logger.LogInformation("OWner" + ownerName.ToString());
                string ownerEmail = json.GetProperty("OwnerEmail").ValueKind switch
                {
                    JsonValueKind.Array => string.Join(", ",
                        json.GetProperty("OwnerEmail").EnumerateArray().Select(x => x.GetString())),
                    _ => json.GetProperty("OwnerEmail").GetString()
                };
                _logger.LogInformation("DL Owner resolved: {Owner}", ownerName);

                return Task.FromResult(new AddOrRemoveDLMembershipDTO
                {
                    DLName = dlName,
                    DLOwner = $"{ownerName} ({ownerEmail})"
                });
            }
            catch (ServiceException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cloud DL lookup failed");

                throw new ServiceException(
                    "Unable to fetch DL details from Exchange Cloud.",
                    ex);
            }
        }
        public string ExchangeHealthCheck()
        {
            try
            {
                _logger.LogInformation("Running Exchange Cloud health check");

                string testCommand = "Get-OrganizationConfig | Select Name | ConvertTo-Json";

                var result = RunExchangeCloudCommand(testCommand);

                if (string.IsNullOrWhiteSpace(result))
                    throw new Exception("Exchange returned empty organization config");

                return "SUCCESS: Exchange Cloud connection working.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exchange health check failed");

                return $"FAILED: {ex.Message}";
            }
        }


        private string SafeSnippet(string input, int length = 200)
        {
            if (string.IsNullOrEmpty(input)) return "NULL/EMPTY";
            return input.Length <= length ? input : input.Substring(0, length) + "...";
        }


        public async Task<AddOrRemoveDLMembershipDTO> GetByDLNameAsync(string dlName)
        {

            //AddOrRemoveDLMembershipDTO dto = new AddOrRemoveDLMembershipDTO()
            //{
            //    DLName = "Finance_DL",
            //    DLOwner = "AYUSH.KAMBLE",
            //    Action = "Add",
            //    //Rights = "SendAs",
            //    Source = "Cloud",
            //    SendMailAccess = "Allow",
            //    GroupOwnershipChange = "Deny"
            //};
            //return dto;
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

                string groupCommand = _cmd.Membership.OnPrem.Get.Replace("{dlName}", dlName);
               // string groupCommand = $"Get-ADGroup -Identity \"{dlName}\" -Properties ManagedBy | ConvertTo-Json -Depth 5";

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

                string ownerCommand = _cmd.Membership.OnPrem.ManagedBy.Replace("{managedByDn}", managedByDn);
    //            string ownerCommand =
    //$"Get-ADUser -Identity \"{managedByDn}\" -Properties DisplayName,SamAccountName | " +
    //                "Select-Object DisplayName,SamAccountName | ConvertTo-Json";

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

        //public async Task<AddOrRemoveDLMembershipDTO> GetByDLNameCloudAsync(string dlName)
        //{
        //    using var scope = _logger.BeginScope("GetByDLNameCloudAsync | DLName: {DLName}", dlName);

        //    try
        //    {
        //        _logger.LogInformation("Method started.");

        //        if (string.IsNullOrWhiteSpace(dlName))
        //        {
        //            _logger.LogWarning("DL name is null or empty.");
        //            throw new ArgumentException("DL name cannot be null or empty");
        //        }

        //        // 🔹 DEBUG MODE — return fake data, skip PowerShell
        //        if ("1y" == "y")
        //        {
        //            _logger.LogWarning("DEBUG MODE enabled. Returning mocked DL owner.");
        //            return new AddOrRemoveDLMembershipDTO
        //            {
        //                DLName = dlName,
        //                DLOwner = "Debug Owner (ILOM00000)"
        //            };
        //        }

        //        /* ===============================
        //           STEP 1: GET AD GROUP
        //        ================================*/

        //        _logger.LogInformation("Executing Get-ADGroup command.");

        //        string groupCommand = _cmd.Membership.Cloud.Get.Replace("{dlName}", dlName);
        //        //string groupCommand =
        //        //    $"Get-ADGroup -Identity \"{dlName}\" -Properties ManagedBy | ConvertTo-Json -Depth 5";

        //        _logger.LogDebug("Group PS Command: {Command}", groupCommand);

        //        var groupResult = Powershell.RunPowerShell(groupCommand);

        //        _logger.LogDebug(
        //            "Group PS Result Length: {Length}, Snippet: {Snippet}",
        //            groupResult?.Length ?? 0,
        //            SafeSnippet(groupResult)
        //        );

        //        if (string.IsNullOrEmpty(groupResult))
        //        {
        //            _logger.LogError("Empty response from Get-ADGroup. Possible AD access or DL not found.");
        //            throw new Exception("Group not found");
        //        }

        //        JsonElement groupJson;
        //        try
        //        {
        //            groupJson = JsonSerializer.Deserialize<JsonElement>(groupResult);
        //        }
        //        catch (Exception ex)
        //        {
        //            _logger.LogError(ex, "Failed to deserialize Get-ADGroup JSON.");
        //            throw;
        //        }

        //        if (!groupJson.TryGetProperty("ManagedBy", out var managedByProp))
        //        {
        //            _logger.LogWarning("ManagedBy property missing in AD group response.");
        //            return new AddOrRemoveDLMembershipDTO
        //            {
        //                DLName = dlName,
        //                DLOwner = "No Owner Assigned"
        //            };
        //        }

        //        string managedByDn = managedByProp.GetString();

        //        _logger.LogInformation("ManagedBy DN resolved: {ManagedByDn}", managedByDn);

        //        if (string.IsNullOrEmpty(managedByDn))
        //        {
        //            _logger.LogInformation("ManagedBy DN is empty. DL has no owner.");
        //            return new AddOrRemoveDLMembershipDTO
        //            {
        //                DLName = dlName,
        //                DLOwner = "No Owner Assigned"
        //            };
        //        }

        //        /* ===============================
        //           STEP 2: GET OWNER DETAILS
        //        ================================*/

        //        _logger.LogInformation("Executing Get-ADUser command.");

        //        string ownerCommand = _cmd.Membership.Cloud.ManagedBy.Replace("{managedByDn}", managedByDn);
        //        //string ownerCommand =
        //        //    $"Get-ADUser -Identity \"{managedByDn}\" -Properties DisplayName,SamAccountName | " +
        //        //    "Select-Object DisplayName,SamAccountName | ConvertTo-Json";

        //        _logger.LogDebug("Owner PS Command: {Command}", ownerCommand);

        //        var ownerResult = Powershell.RunPowerShell(ownerCommand);

        //        _logger.LogDebug(
        //            "Owner PS Result Length: {Length}, Snippet: {Snippet}",
        //            ownerResult?.Length ?? 0,
        //            SafeSnippet(ownerResult)
        //        );

        //        if (string.IsNullOrEmpty(ownerResult))
        //        {
        //            _logger.LogError("Empty response from Get-ADUser. Owner DN may be invalid.");
        //            throw new Exception("Owner details not found");
        //        }

        //        JsonElement ownerJson;
        //        try
        //        {
        //            ownerJson = JsonSerializer.Deserialize<JsonElement>(ownerResult);
        //        }
        //        catch (Exception ex)
        //        {
        //            _logger.LogError(ex, "Failed to deserialize Get-ADUser JSON.");
        //            throw;
        //        }

        //        string displayName = ownerJson.GetProperty("DisplayName").GetString();
        //        string samAccountName = ownerJson.GetProperty("SamAccountName").GetString();

        //        _logger.LogInformation(
        //            "DL owner fetched successfully. DisplayName: {DisplayName}, SamAccountName: {Sam}",
        //            displayName,
        //            samAccountName
        //        );

        //        return new AddOrRemoveDLMembershipDTO
        //        {
        //            DLName = dlName,
        //            DLOwner = $"{displayName} ({samAccountName})"
        //        };
        //    }
        //    catch (ServiceException)
        //    {
        //        _logger.LogWarning("ServiceException occurred. Rethrowing.");
        //        throw;
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Unhandled exception in GetByDLNameCloudAsync.");
        //        throw new ServiceException("Unable to fetch DL details.", ex);
        //    }
        //}


        public async Task<string> TestDBConnectionAsync()
        {
            try
            {
                // STEP 1 — Open connection
                _logger.LogInformation("STEP 1 → Opening DB connection...");

                if (_context.Database.GetDbConnection().State != System.Data.ConnectionState.Open)
                    await _context.Database.OpenConnectionAsync();

                _logger.LogInformation("✅ Connection opened successfully");


                // STEP 2 — Simple SELECT test
                _logger.LogInformation("STEP 2 → Running SELECT test...");

                var testValue = await _context.DLDetails
                                              .AsNoTracking()
                                              .Select(x => x.ApprovalID)
                                              .FirstOrDefaultAsync();

                _logger.LogInformation("✅ SELECT query successful");


                // STEP 3 — INSERT test record
                _logger.LogInformation("STEP 3 → Preparing INSERT test record...");

                var testRecord = new DL
                {
                    ApprovalID = Guid.NewGuid().ToString(),
                    Event = "DB_TEST",
                    DLName = "TEST_CONNECTION",
                    DLOWner = "SYSTEM",
                    Action = "TEST",
                    EmpIds = "TEST",
                    Rights = "",
                    IsLocked = false,
                    CreatedTime = DateTime.UtcNow,
                    ApprovalStatus = "Test",
                    ApproverEmail = ""
                };


                _logger.LogInformation("STEP 4 → Adding entity...");
                await _context.DLDetails.AddAsync(testRecord);


                // STEP 4 — SaveChanges test
                _logger.LogInformation("STEP 5 → Saving to DB...");
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ INSERT successful. ApprovalID: {Id}", testRecord.ApprovalID);


                return $"SUCCESS ✅ | Connection + Read + Write working | TestID: {testRecord.ApprovalID}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ DB TEST FAILED");

                return $"FAILED ❌ | {ex.GetType().Name} | {ex.Message}";
            }
        }

        public async Task<bool> AddOrRemoveDLMembershipAsync(AddOrRemoveDLMembershipDTO dto)
        {
            if (dto == null)
                throw new ServiceException("Invalid request payload.");

            using var scope = _logger.BeginScope(new { dto.DLName, dto.Action });
            try
            {
                ValidateMembershipRequest(dto);
                ResolveRequestor(dto);

                // The DL owner is the approver, so it is always re-resolved from the directory
                // instead of trusting the value posted by the client (prevents approver tampering).
                _logger.LogInformation("Resolving DL owner from directory...");
                dto.DLOwner = await ResolveDLOwnerAsync(dto);

                if (_recipientResolver.ResolveOwnerEmails(dto.DLOwner).Count == 0)
                    throw new ServiceException("Unable to determine the DL owner's email address. Approval email cannot be sent.");

                _logger.LogInformation("Saving DL request into DB...");
                DL? saved = await SaveToDB(dto);

                if (saved == null)
                    throw new ServiceException("Failed to save request in DB.");

                _logger.LogInformation("Sending approval email to DL owner...");

                bool emailSent = await _notificationService.SendApprovalRequestAsync(saved);

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

        // Characters that would break out of / expand inside the double-quoted PowerShell templates
        // (straight and typographic double quotes, backtick, dollar) plus control characters.
        private static readonly char[] UnsafeDLNameChars = { '"', '“', '”', '„', '`', '$' };

        private static void ValidateMembershipRequest(AddOrRemoveDLMembershipDTO dto)
        {
            dto.DLName = dto.DLName?.Trim();
            if (string.IsNullOrWhiteSpace(dto.DLName) || dto.DLName.Length > 256)
                throw new ServiceException("DL name is required.");

            if (dto.DLName.IndexOfAny(UnsafeDLNameChars) >= 0 || dto.DLName.Any(char.IsControl))
                throw new ServiceException("DL name contains invalid characters.");

            // Canonicalise Action/Source: downstream code compares them case-sensitively
            // ("Add" adds, anything else removes; "OnPrem"/"Cloud" selects the event).
            if (string.Equals(dto.Action?.Trim(), "Add", StringComparison.OrdinalIgnoreCase))
                dto.Action = "Add";
            else if (string.Equals(dto.Action?.Trim(), "Remove", StringComparison.OrdinalIgnoreCase))
                dto.Action = "Remove";
            else
                throw new ServiceException("Action must be either 'Add' or 'Remove'.");

            if (string.Equals(dto.Source?.Trim(), "OnPrem", StringComparison.OrdinalIgnoreCase))
                dto.Source = "OnPrem";
            else if (string.Equals(dto.Source?.Trim(), "Cloud", StringComparison.OrdinalIgnoreCase))
                dto.Source = "Cloud";
            else
                throw new ServiceException("Invalid source value. Allowed: OnPrem, Cloud.");

            var empIds = EmailRecipientResolver.SplitEmployeeIds(dto.EmpIds);
            if (empIds.Count == 0)
                throw new ServiceException("At least one Employee/Consultant ID is required.");

            if (!empIds.All(EmailRecipientResolver.IsValidEmployeeId))
                throw new ServiceException("One or more Employee/Consultant IDs are invalid. Only letters, digits, '.', '_' and '-' are allowed.");

            dto.EmpIds = string.Join(",", empIds);
        }

        /// <summary>
        /// Requestor = currently logged-in user. When the API runs with an authenticated identity
        /// (e.g. Windows Authentication on IIS) that identity wins; otherwise the value sent by the
        /// frontend is used after strict validation.
        /// </summary>
        private void ResolveRequestor(AddOrRemoveDLMembershipDTO dto)
        {
            var identity = _httpContextAccessor.HttpContext?.User?.Identity;

            if (identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(identity.Name))
            {
                string authId = identity.Name;

                int slash = authId.LastIndexOf('\\');           // DOMAIN\user
                if (slash >= 0) authId = authId[(slash + 1)..];

                int at = authId.IndexOf('@');                     // user@domain
                if (at > 0) authId = authId[..at];

                if (!string.IsNullOrWhiteSpace(dto.RequestorId) &&
                    !string.Equals(dto.RequestorId.Trim(), authId, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Submitted requestor differs from authenticated user. Authenticated identity will be used.");
                }

                dto.RequestorId = authId;
            }

            dto.RequestorId = dto.RequestorId?.Trim();
            if (!EmailRecipientResolver.IsValidEmployeeId(dto.RequestorId))
                throw new ServiceException("Requestor ID is missing or invalid.");

            dto.RequestorName = dto.RequestorName?.Trim();
            if (string.IsNullOrWhiteSpace(dto.RequestorName))
                dto.RequestorName = dto.RequestorId;

            if (dto.RequestorName!.Length > 256 || dto.RequestorName.Any(char.IsControl))
                throw new ServiceException("Requestor name is invalid.");
        }

        private async Task<string> ResolveDLOwnerAsync(AddOrRemoveDLMembershipDTO dto)
        {
            AddOrRemoveDLMembershipDTO ownerInfo = dto.Source == "OnPrem"
                ? await GetByDLNameAsync(dto.DLName!)
                : await GetByDLNameCloudAsync(dto.DLName!);

            string? resolvedOwner = ownerInfo?.DLOwner?.Trim();

            if (string.IsNullOrWhiteSpace(resolvedOwner) ||
                string.Equals(resolvedOwner, "No Owner Assigned", StringComparison.OrdinalIgnoreCase))
            {
                throw new ServiceException("No owner is assigned to this DL. Approval email cannot be sent.");
            }

            if (!string.IsNullOrWhiteSpace(dto.DLOwner) &&
                !string.Equals(dto.DLOwner.Trim(), resolvedOwner, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Submitted DL owner differs from directory owner. Directory value will be used.");
            }

            return resolvedOwner;
        }

        private async Task<DL?> SaveToDB(AddOrRemoveDLMembershipDTO dto)
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
                        ApproverEmail = "",
                        RequestorId = dto.RequestorId,
                        RequestorName = dto.RequestorName
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
                        ApproverEmail = "",
                        RequestorId = dto.RequestorId,
                        RequestorName = dto.RequestorName
                    };
                }


                await _context.DLDetails.AddAsync(dl);
                await _context.SaveChangesAsync();

                _logger.LogInformation("DL request saved successfully. ApprovalID: {Id}", dl.ApprovalID);
                return dl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database save failed for DL: {DLName}", dto.DLName);
                return null;
            }
        }
    }
}
