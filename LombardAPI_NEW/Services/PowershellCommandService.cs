using LombardAPI_NEW.Models;
using LombardAPI_NEW.Shared;
using Microsoft.Extensions.Options;
using System.Text.Json;

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


        //public void GroupMembershipCommand(DL req)
        //{
        //    using var scope = _logger.BeginScope(
        //        "GroupMembership | DL: {DL}, Event: {Event}, Action: {Action}",
        //        req.DLName, req.Event, req.Action);

        //    if (string.IsNullOrWhiteSpace(req.DLName) || string.IsNullOrWhiteSpace(req.EmpIds))
        //    {
        //        _logger.LogError("DLName or EmpIds missing.");
        //        throw new ArgumentException("DLName and EmpIds are required.");
        //    }

        //    var users = req.EmpIds.Split(',', StringSplitOptions.RemoveEmptyEntries);

        //    _logger.LogInformation("Processing membership for {UserCount} users.", users.Length);

        //    foreach (var user in users)
        //    {
        //        string template = req.Event switch
        //        {
        //            "Membership | OnPrem" => req.Action == "Add"
        //                ? _cmd.Membership.OnPrem.Add
        //                : _cmd.Membership.OnPrem.Remove,

        //            "Membership | Cloud" => req.Action == "Add"
        //                ? _cmd.Membership.Cloud.Add
        //                : _cmd.Membership.Cloud.Remove,

        //            _ => throw new InvalidOperationException("Invalid Membership Event")
        //        };

        //        string psCmd = template
        //            .Replace("{DLName}", req.DLName)
        //            .Replace("{User}", user.Trim());

        //        _logger.LogInformation(
        //            "Executing membership command for user: {User}", user);

        //        _logger.LogDebug("PowerShell Command: {Command}", psCmd);

        //        try
        //        {
        //            Powershell.RunPowerShell(psCmd);
        //            _logger.LogInformation("Membership operation succeeded for user: {User}", user);
        //        }
        //        catch (Exception ex)
        //        {
        //            _logger.LogError(
        //                ex,
        //                "Membership operation failed for user: {User}", user);
        //            throw;
        //        }
        //    }

        //    _logger.LogInformation("Group membership processing completed.");
        //}
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

                string psCmd = template
                    .Replace("{DLName}", req.DLName)
                    .Replace("{User}", user);

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

            _logger.LogInformation("GroupMembershipCommand completed successfully.");
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
