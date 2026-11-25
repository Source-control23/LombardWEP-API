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
        private readonly CurrentUserService _currentUser;

        public GroupMembershipService(CommonService commonService,
            DataBaseContext context,
            IEmailService emailService,
            ILogger<GroupMembershipService> logger, IOptions<PowerShellCommandSettings> options,
            IMembershipNotificationService notificationService,
            EmailRecipientResolver recipientResolver,
            CurrentUserService currentUser)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
            _cmd = options.Value;
            _commonService = commonService;
            _notificationService = notificationService;
            _recipientResolver = recipientResolver;
            _currentUser = currentUser;
        }

        public string RunExchangeCloudCommand(string command)
        {
            using var scope = _logger.BeginScope("ExchangeCloud | Command: {Cmd}", LogSanitizer.Clean(command));

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
            // CWE-117 Log Forging: strip CR/LF/control chars from user input at entry.
            dlName = System.Text.RegularExpressions.Regex.Replace(dlName ?? string.Empty, @"[\r\n\t\p{Cc}]", string.Empty);
            using var scope = _logger.BeginScope("Cloud DL Lookup | {DL}", LogSanitizer.Clean(dlName));

            try
            {
                if (string.IsNullOrWhiteSpace(dlName))
                    throw new ServiceException("DL name cannot be empty.");

                // PowerShell injection guard (defence in depth): the name is placed inside a
                // double-quoted template, so quotes, backtick and '$' are rejected.
                dlName = RequestValidation.NormaliseDLName(dlName);

                _logger.LogInformation("STEP 1 → Preparing PowerShell command");

                string command = _cmd.Membership.Cloud.GetDLWithOwner
                    .Replace("{dlName}", dlName);

                _logger.LogDebug("Exchange Command: {Command}", LogSanitizer.Clean(command));

                _logger.LogInformation("STEP 2 → Executing Exchange lookup");

                var result = RunExchangeCloudCommand(command);

                _logger.LogInformation("STEP 3 → Parsing Exchange result: {Result}", LogSanitizer.Clean(result?.ToString()));

                var json = JsonSerializer.Deserialize<JsonElement>(result);
                _logger.LogInformation("STEP 4 → Parsing result as json: {Json}", LogSanitizer.Clean(json.ToString()));
                string ownerName = json.GetProperty("OwnerName").ValueKind switch
                {
                    JsonValueKind.Array => string.Join(", ",
                        json.GetProperty("OwnerName").EnumerateArray().Select(x => x.GetString())),
                    _ => json.GetProperty("OwnerName").GetString()
                };
                _logger.LogInformation("Owner: {Owner}", LogSanitizer.Clean(ownerName?.ToString()));
                string ownerEmail = json.GetProperty("OwnerEmail").ValueKind switch
                {
                    JsonValueKind.Array => string.Join(", ",
                        json.GetProperty("OwnerEmail").EnumerateArray().Select(x => x.GetString())),
                    _ => json.GetProperty("OwnerEmail").GetString()
                };

                // Prefer the owner's sign-in name (UPN, e.g. ILOM35124@icicilombard.com) when the
                // lookup returns it: the primary SMTP address can differ (e.g. akash.gowda@ext...),
                // and the approval page matches the approver against the signed-in UPN.
                // Falls back to OwnerEmail when the configured command does not return OwnerUpn.
                if (json.TryGetProperty("OwnerUpn", out var upnProp))
                {
                    string? ownerUpn = upnProp.ValueKind switch
                    {
                        JsonValueKind.Array => string.Join(", ",
                            upnProp.EnumerateArray().Select(x => x.GetString()).Where(s => !string.IsNullOrWhiteSpace(s))),
                        JsonValueKind.String => upnProp.GetString(),
                        _ => null
                    };

                    if (!string.IsNullOrWhiteSpace(ownerUpn))
                        ownerEmail = ownerUpn;
                }

                _logger.LogInformation("DL Owner resolved: {Owner}", LogSanitizer.Clean(ownerName));

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
            // CWE-117 Log Forging: strip CR/LF/control chars from user input at entry.
            dlName = System.Text.RegularExpressions.Regex.Replace(dlName ?? string.Empty, @"[\r\n\t\p{Cc}]", string.Empty);
            using var scope = _logger.BeginScope("GetByDLNameAsync | DLName: {DLName}", LogSanitizer.Clean(dlName));

            try
            {
                _logger.LogInformation("Method started.");

                if (string.IsNullOrWhiteSpace(dlName))
                {
                    _logger.LogWarning("DL name is null or empty.");
                    throw new ArgumentException("DL name cannot be null or empty");
                }

                // PowerShell injection guard (defence in depth): the name is placed inside a
                // double-quoted template, so quotes, backtick and '$' are rejected.
                dlName = RequestValidation.NormaliseDLName(dlName);

                _logger.LogInformation("Executing Get-ADGroup command.");

                string groupCommand = _cmd.Membership.OnPrem.Get.Replace("{dlName}", dlName);
               // string groupCommand = $"Get-ADGroup -Identity \"{dlName}\" -Properties ManagedBy | ConvertTo-Json -Depth 5";

                _logger.LogDebug("Group PS Command: {Command}", LogSanitizer.Clean(groupCommand));

                var groupResult = Powershell.RunPowerShell(groupCommand);
                _logger.LogInformation("GroupResult from cmd : {GroupResult}", LogSanitizer.Clean(groupResult));
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

                _logger.LogInformation("ManagedBy DN: {ManagedByDn}", LogSanitizer.Clean(managedByDn));

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

                _logger.LogDebug("Owner PS Command: {Command}", LogSanitizer.Clean(ownerCommand));

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

        //        _logger.LogDebug("Owner PS Command: {Command}", LogSanitizer.Clean(ownerCommand));

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

                // Improper Error Handling: details stay in the server log only.
                return "FAILED ❌ | Database test failed. See server logs for details.";
            }
        }

        public async Task<bool> AddOrRemoveDLMembershipAsync(AddOrRemoveDLMembershipDTO dto)
        {
            if (dto == null)
                throw new ServiceException("Invalid request payload.");

            using var scope = _logger.BeginScope(new { DLName = LogSanitizer.Clean(dto.DLName), Action = LogSanitizer.Clean(dto.Action) });
            try
            {
                ValidateMembershipRequest(dto);
                ResolveRequestor(dto);

                // The DL owner is the approver, so it is always re-resolved from the directory
                // instead of trusting the value posted by the client (prevents approver tampering).
                _logger.LogInformation("Resolving DL owner from directory...");
                dto.DLOwner = await ResolveDLOwnerAsync(dto);

                var ownerEmails = _recipientResolver.ResolveOwnerEmails(dto.DLOwner);
                if (ownerEmails.Count == 0)
                    throw new ServiceException("Unable to determine the DL owner's email address. Approval email cannot be sent.");

                _logger.LogInformation("Saving DL request into DB...");
                // ApproverEmail = the only people allowed to approve/reject this request.
                DL? saved = await SaveToDB(dto, string.Join(",", ownerEmails));

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

        private static void ValidateMembershipRequest(AddOrRemoveDLMembershipDTO dto)
        {
            dto.DLName = RequestValidation.NormaliseDLName(dto.DLName);
            dto.Action = RequestValidation.NormaliseAction(dto.Action);
            dto.Source = RequestValidation.NormaliseSource(dto.Source);
            dto.EmpIds = RequestValidation.NormaliseEmployeeIds(dto.EmpIds);
        }

        /// <summary>
        /// Requestor = currently logged-in user (validated Entra ID token when present,
        /// otherwise the value sent by the frontend after strict validation).
        /// </summary>
        private void ResolveRequestor(AddOrRemoveDLMembershipDTO dto)
        {
            var (id, name) = _currentUser.ResolveRequestor(dto.RequestorId, dto.RequestorName);
            dto.RequestorId = id;
            dto.RequestorName = name;
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

        private async Task<DL?> SaveToDB(AddOrRemoveDLMembershipDTO dto, string approverEmails)
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
                        ApproverEmail = approverEmails,
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
                        ApproverEmail = approverEmails,
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
                // CWE-117 Log Forging: inline CR/LF/control-char strip on the user-supplied value.
                string safeDlName = System.Text.RegularExpressions.Regex.Replace(dto.DLName ?? string.Empty, @"[\r\n\t\p{Cc}]", string.Empty);
                _logger.LogError(ex, "Database save failed for DL: {DLName}", safeDlName);
                return null;
            }
        }
    }
}
