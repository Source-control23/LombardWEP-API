using LombardAPI_NEW.ErrorHandling;
using LombardAPI_NEW.Models;
using LombardAPI_NEW.Models.DTOs;
using LombardAPI_NEW.Shared;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Xml.Linq;

namespace LombardAPI_NEW.Services
{
  public class PowershellCommandService
    {
        private readonly PowerShellCommandSettings _cmd;
        private readonly ILogger<PowershellCommandService> _logger;
        public PowershellCommandService(
            IOptions<PowerShellCommandSettings> options,
            ILogger<PowershellCommandService> logger)
        {
            _cmd = options.Value;
            _logger = logger;
        }
        
        public void GroupMembershipCommand(DL req)
        {
            using var scope = _logger.BeginScope(
                "GroupMembership | DL: {DL}, Event: {Event}, Action: {Action}",
                req.DLName, req.Event, req.Action);

            _logger.LogInformation("GroupMembershipCommand started.");

            if (string.IsNullOrWhiteSpace(req.DLName) || string.IsNullOrWhiteSpace(req.EmpIds))
            {
                _logger.LogError(
                    "Validation failed. DLName or EmpIds is missing. DLName: {DLName}, EmpIds: {EmpIds}",
                    req.DLName, req.EmpIds);

                throw new ArgumentException("DLName and EmpIds are required.");
            }

            var users = req.EmpIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(u => u.Trim())
                .ToArray();

            _logger.LogInformation(
                "Parsed users successfully. Total users to process: {UserCount}",
                users.Length);

            for (int i = 0; i < users.Length; i++)
            {
                var user = users[i];

                _logger.LogInformation(
                    "Processing user {CurrentIndex}/{TotalUsers}. User: {User}",
                    i + 1, users.Length, user);

                string template;
                try
                {
                    template = req.Event switch
                    {
                        "Membership | OnPrem" => req.Action == "Add"
                            ? _cmd.Membership.OnPrem.Add
                            : _cmd.Membership.OnPrem.Remove,

                        "Membership | Cloud" => req.Action == "Add"
                            ? _cmd.Membership.Cloud.Add
                            : _cmd.Membership.Cloud.Remove,

                        _ => throw new InvalidOperationException(
                            $"Invalid Membership Event: {req.Event}")
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to resolve PowerShell template. Event: {Event}, Action: {Action}",
                        req.Event, req.Action);
                    throw;
                }
                if (req.Event == "Membership | OnPrem")
                {
                    string psCmd = template
                  .Replace("{DLName}", req.DLName)
                  .Replace("{User}", user);
                    _logger.LogInformation("Using on-premises membership command template.");

                    _logger.LogDebug("Resolved PowerShell command: {PowerShellCommand}", psCmd);

                    try
                    {
                        _logger.LogInformation("Executing PowerShell command.");
                        Powershell.RunPowerShell(psCmd);
                        _logger.LogInformation("Membership operation succeeded for user: {User}", user);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Membership operation failed. User: {User}, DL: {DL}, Command: {Command}",
                            user, req.DLName, psCmd);
                        throw;
                    }
                }
                else
                {
                    string groupCommand = "";
                    if (req.Action == "Add")
                    {
                        groupCommand = _cmd.Membership.Cloud.Add
     .Replace("{DLName}", req.DLName)
     .Replace("{User}", req.EmpIds);
                    }
                    else
                    {
                        groupCommand = _cmd.Membership.Cloud.Remove
                        .Replace("{DLName}", req.DLName)
                        .Replace("{User}", req.EmpIds);
                    }

                    var groupResult = RunExchangeCloudCommand(groupCommand);
                    if (!string.IsNullOrWhiteSpace(groupResult))
                    {
                        _logger.LogInformation("Group command executed successfully. Proceeding with membership command.");
                    }
                    else
                    {
                        _logger.LogError("Group command execution failed. Aborting membership command.");
                        throw new Exception("Failed to execute group command. Cannot proceed with membership changes.");
                    }
                    _logger.LogInformation("Using cloud membership command template.");
                }            
            }
            _logger.LogInformation("GroupMembershipCommand completed successfully.");
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

                var connectResult = Powershell.RunPowerShellCloudConnect(connectScript, command);

                

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
        public void RightsManagementCommand(DL req)
        {
            using var scope = _logger.BeginScope(
                "RightsManagement | DL: {DL}, Event: {Event}, Action: {Action}",
                req.DLName, req.Event, req.Action);

            if (string.IsNullOrWhiteSpace(req.DLName) || string.IsNullOrWhiteSpace(req.Rights))
            {
                _logger.LogError("DLName or Rights missing.");
                throw new ArgumentException("DLName and Rights are required.");
            }

            string template = req.Event switch
            {
                "Rights | OnPrem" => req.Action == "Add"
                    ? _cmd.Rights.OnPrem.Add
                    : _cmd.Rights.OnPrem.Remove,

                "Rights | Cloud" => req.Action == "Add"
                    ? _cmd.Rights.Cloud.Add
                    : _cmd.Rights.Cloud.Remove,

                _ => throw new InvalidOperationException("Invalid Rights Event")
            };

            string psCmd = template
                .Replace("{DLName}", req.DLName)
                .Replace("{Rights}", req.Rights);

            _logger.LogInformation("Executing rights management command.");
            _logger.LogDebug("PowerShell Command: {Command}", psCmd);

            if (req.Event == "Rights | OnPrem")
            {
                try
                {
                    Powershell.RunPowerShell(psCmd);
                    _logger.LogInformation("Rights management operation succeeded.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Rights management operation failed.");
                    throw;
                }
            }
            else
            {
                string groupCommand = "";
                if (req.Action == "Add")
                {
                    groupCommand = _cmd.Rights.Cloud.Add
                    .Replace("{DLName}", req.DLName)
                    .Replace("{User}", req.Rights);
                }
                else
                {
                    groupCommand = _cmd.Rights.Cloud.Remove
                    .Replace("{DLName}", req.DLName)
                    .Replace("{User}", req.Rights);
                }

                var groupResult = RunExchangeCloudCommand(groupCommand);
                if (!string.IsNullOrWhiteSpace(groupResult))
                {
                    _logger.LogInformation("Group command executed successfully. Proceeding with membership command.");
                }
                else
                {
                    _logger.LogError("Group command execution failed. Aborting membership command.");
                    throw new Exception("Failed to execute group command. Cannot proceed with membership changes.");
                }
                _logger.LogInformation("Using cloud membership command template.");
            }
        }
        public void AccountUnlockCommand(DL req)
        {
            using var scope = _logger.BeginScope(
                "AccountUnlock | EmployeeId: {EmpId}", req.EmpIds);

            if (string.IsNullOrWhiteSpace(req.EmpIds))
            {
                _logger.LogError("EmployeeId missing.");
                throw new ArgumentException("EmployeeId is required.");
            }

            string unlockCmd = _cmd.AccountUnlock.Unlock
                .Replace("{EmpId}", req.EmpIds);

            _logger.LogInformation("Executing account unlock command.");
            _logger.LogDebug("PowerShell Unlock Command: {Command}", unlockCmd);

            try
            {
                Powershell.RunPowerShell(unlockCmd);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Account unlock PowerShell command failed.");
                throw;
            }

            string verifyCmd = _cmd.AccountUnlock.Verify
                .Replace("{EmpId}", req.EmpIds);

            _logger.LogInformation("Verifying account unlock status.");
            _logger.LogDebug("PowerShell Verify Command: {Command}", verifyCmd);

            string resultJson;
            try
            {
                resultJson = Powershell.RunPowerShell(verifyCmd);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Account unlock verification failed.");
                throw;
            }

            _logger.LogDebug("Verification output: {Output}", resultJson);

            if (!string.IsNullOrWhiteSpace(resultJson))
            {
                var user = JsonSerializer.Deserialize<JsonElement>(resultJson);

                if (user.TryGetProperty("LockedOut", out var locked) && locked.GetBoolean())
                {
                    _logger.LogError("Account unlock verification failed. User still locked.");
                    throw new Exception("Account unlock failed. User is still locked.");
                }
            }

            _logger.LogInformation("Account unlocked and verified successfully.");
        }
    }

}
