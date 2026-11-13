using LombardAPI_NEW.DataBase;
using LombardAPI_NEW.Interfaces;
using LombardAPI_NEW.Models;
using LombardAPI_NEW.Models.DTOs;
using LombardAPI_NEW.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace LombardAPI_NEW.Services
{
    public class AccountUnlockService : IAccountUnlock
    {
        private readonly DataBaseContext _context;
        private readonly IEmailService _emailService;
        private readonly PowerShellCommandSettings _cmd;
        private readonly ILogger<AccountUnlockService> _logger;
        private readonly CommonService _commonService;

        public AccountUnlockService(CommonService commonService, DataBaseContext context,IEmailService emailService,IOptions<PowerShellCommandSettings> options,ILogger<AccountUnlockService> logger)
        {
            _context = context;
            _emailService = emailService;
            _cmd = options.Value;
            _logger = logger;
            _commonService = commonService;
        }
        public async Task<AccountUnlockResponseDTO> GetUserDetailsAsync(string employeeId)
        {
            //var mock = new AccountUnlockResponseDTO
            //{
            //    EmployeeId = employeeId,
            //    IsLocked = true,
            //    AccountStatus = "Disabled",
            //    AccountExpiry = "Never",
            //    PasswordStatus = "Password Expired",
            //    Info = "Debug endpoint response"
            //};

            //return mock;

            using var scope = _logger.BeginScope(
                "AccountUnlock | EmployeeId: {EmployeeId}", employeeId);

            var psCmd = _cmd.AccountUnlock.Verify.Replace("{EmpId}", employeeId);
            _logger.LogInformation("Executing AccountUnlock verification command.");
            _logger.LogDebug("PowerShell Command: {Command}", psCmd);

            string output;
            try
            {
                output = Powershell.RunPowerShell(psCmd);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PowerShell execution failed.");
                return null;
            }
            _logger.LogDebug("PowerShell Command Output: " + output);

            _logger.LogDebug(
                "PowerShell output length: {Length}, snippet: {Snippet}",
                output?.Length ?? 0,
                output?.Length > 200 ? output.Substring(0, 200) : output
            );

            if (string.IsNullOrWhiteSpace(output))
            {
                _logger.LogWarning("Empty PowerShell output.");
                return null;
            }

            if (!LooksLikeJson(output))
            {
                _logger.LogError("PowerShell output is not JSON. Output: {Output}", output);
                return null;
            }

            if (!TryParseUser(output, out var user))
            {
                _logger.LogError("Failed to parse JSON from PowerShell output.");
                return null;
            }

            if (!user.TryGetProperty("SamAccountName", out var samProp))
            {
                _logger.LogError("SamAccountName missing. Full JSON: {Json}", user.ToString());
                return null;
            }

            if (!user.TryGetProperty("LockedOut", out var lockedProp))
            {
                _logger.LogError("LockedOut property missing. Full JSON: {Json}", user.ToString());
                return null;
            }

            bool locked = lockedProp.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ when bool.TryParse(lockedProp.ToString(), out var parsed) => parsed,
                _ => false
            };

            _logger.LogInformation(
                "Account status resolved. SamAccountName: {Sam}, Locked: {Locked}",
                samProp.GetString(), locked);
            string sam = samProp.GetString() ?? string.Empty;

            // ---------- Account Status ----------
            string accountStatus = "Unknown";

            if (user.TryGetProperty("UserAccountControl", out var uacProp))
            {
                int uac = 0;
                int.TryParse(uacProp.ToString(), out uac);

                accountStatus = uac switch
                {
                    512 => "Enabled",
                    514 => "Disabled",
                    _ => "Unknown"
                };
            }
            else if (user.TryGetProperty("Enabled", out var enabledProp))
            {
                accountStatus = enabledProp.GetBoolean() ? "Enabled" : "Disabled";
            }

            // ---------- Info ----------
            string info = user.TryGetProperty("Info", out var infoProp)
                ? infoProp.ToString()
                : "";
            _logger.LogInformation($"{info}", info);
            // ---------- Account Expiry ----------
            string accountExpiry = "Never";

            if (user.TryGetProperty("AccountExpires", out var expProp))
            {
                if (long.TryParse(expProp.ToString(), out var fileTime))
                {
                    var safeDate = SafeFromFileTime(fileTime);

                    if (safeDate.HasValue)
                        accountExpiry = safeDate.Value.ToString("dd MMM yyyy");
                }
            }

            // ---------- Password Expiry ----------
            string passwordStatus = "Unknown";

            if (user.TryGetProperty("PasswordLastSet", out var pwdProp))
            {
                if (DateTime.TryParse(pwdProp.ToString(), out var lastSet))
                {
                    var expiry = lastSet.AddDays(45);// from 60 to 45 days as per policy 

                    passwordStatus = expiry < DateTime.UtcNow
                        ? "Password Expired"
                        : expiry.ToString("dd MMM yyyy");
                }
            }



            return new AccountUnlockResponseDTO
            {
                EmployeeId = sam,
                IsLocked = locked,
                AccountStatus = accountStatus,
                AccountExpiry = accountExpiry,
                PasswordStatus = passwordStatus,
                Info = info
            };

        }
        private bool LooksLikeJson(string input)
        {
            var trimmed = input.TrimStart();
            return trimmed.StartsWith("{") || trimmed.StartsWith("[");
        }
        private static DateTime? SafeFromFileTime(long fileTime)
        {
            // AD special values or invalid range
            if (fileTime <= 0 || fileTime == long.MaxValue)
                return null;

            try
            {
                return DateTime.FromFileTimeUtc(fileTime);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }
        public async Task<bool> SaveToDB(AccountUnlockRequestDTO dto)
        {
            try
            {
                var dl = new DL
                {
                    ApprovalID = Guid.NewGuid().ToString(),
                    Event = "Unlock",
                    DLName = dto.DLName,
                    DLOWner = dto.DLOwner,
                    Action = "",
                    EmpIds = dto.EmployeeId,
                    Rights = "",
                    IsLocked = false,
                    CreatedTime = DateTime.UtcNow,
                    ApprovalStatus = "Completed",
                    ApproverEmail = null
                };

                await _context.DLDetails.AddAsync(dl);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                throw;
            }
        }
        public async Task<ApiResponse> UnlockAccountAsync(AccountUnlockRequestDTO request)
        {
            string unlockCmd = _cmd.AccountUnlock.Unlock
               .Replace("{EmpId}", request.EmployeeId);

            _logger.LogInformation("Executing account unlock command.");
            _logger.LogDebug("PowerShell Unlock Command: {Command}", unlockCmd);

            try
            {
                Powershell.RunPowerShell(unlockCmd);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Account unlock PowerShell command failed.");
                return ApiResponse.Ok(new
                {
                    EmployeeId = request.EmployeeId,
                    Status = "Account Unlocked Failed",
                    ApprovalId = ""
                });
                throw;
            }

            try
            {
                var res = SaveToDB(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save in DB");
                return ApiResponse.Ok(new
                {
                    EmployeeId = request.EmployeeId,
                    Status = "Account Unlocked Failed",
                    ApprovalId = ""
                });
                throw;
            }


            return ApiResponse.Ok(new
            {
                EmployeeId = request.EmployeeId,
                Status = "Account Unlocked",
                ApprovalId = ""
            });
        }
        private string BuildMailBody(string link)
        {
            return $@"
  <div style='font-family:Segoe UI,Arial,sans-serif; font-size:14px; color:#333; line-height:1.6;'>
      
      <p>Dear Team,</p>

      <p>
          A new <strong>Unlock Lock Account Request</strong> requires your review and approval.
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
        }
        private bool TryParseUser(string json, out JsonElement element)
        {
            element = default;
            try
            {
                element = JsonSerializer.Deserialize<JsonElement>(json);
                return element.ValueKind != JsonValueKind.Undefined &&
                       element.ValueKind != JsonValueKind.Null;
            }
            catch
            {
                return false;
            }
        }
    }
}
