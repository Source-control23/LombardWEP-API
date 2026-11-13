using LombardAPI_NEW.ErrorHandling;
using LombardAPI_NEW.Models;
using LombardAPI_NEW.Models.DTOs;
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

        // ─────────────────────────────────────────────────────────────────────────
        // GROUP MEMBERSHIP
        // ─────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Executes Add/Remove for each user and returns the per-user status.
        /// Behaviour is unchanged: the first failing user still throws a ServiceException.
        /// </summary>
        public IReadOnlyList<MembershipExecutionResult> GroupMembershipCommand(DL req)
        {
            using var scope = _logger.BeginScope(
                "GroupMembership | DL: {DL}, Event: {Event}, Action: {Action}",
                req.DLName, req.Event, req.Action);

            _logger.LogInformation("GroupMembershipCommand started.");

            if (string.IsNullOrWhiteSpace(req.DLName) || string.IsNullOrWhiteSpace(req.EmpIds))
                throw new ServiceException("DLName and EmpIds are required.");

            var users = req.EmpIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(u => u.Trim())
                .ToArray();

            _logger.LogInformation("Parsed users successfully. Total users to process: {UserCount}", users.Length);

            var results = new List<MembershipExecutionResult>(users.Length);

            for (int i = 0; i < users.Length; i++)
            {
                var user = users[i];
                _logger.LogInformation("Processing user {CurrentIndex}/{TotalUsers}. User: {User}", i + 1, users.Length, user);

                ProcessSingleMembershipUser(req, user);
                results.Add(new MembershipExecutionResult(user, MembershipExecutionResult.Success));
            }

            _logger.LogInformation("GroupMembershipCommand completed successfully.");
            return results;
        }

        private void ProcessSingleMembershipUser(DL req, string user)
        {
            switch (req.Event)
            {
                case "Membership | OnPrem":
                    ExecuteOnPremMembership(req, user);
                    break;

                case "Membership | Cloud":
                    ExecuteCloudMembership(req, user);
                    break;

                default:
                    _logger.LogError("Unsupported membership event type: {Event}", req.Event);
                    throw new ServiceException($"Unsupported membership event: '{req.Event}'. Expected 'Membership | OnPrem' or 'Membership | Cloud'.");
            }
        }

        private void ExecuteOnPremMembership(DL req, string user)
        {
            string template = req.Action == "Add"
                ? _cmd.Membership.OnPrem.Add
                : _cmd.Membership.OnPrem.Remove;

            string psCmd = template
                .Replace("{DLName}", req.DLName)
                .Replace("{User}", user);

            _logger.LogInformation("Using on-premises membership command template. Action: {Action}, User: {User}", req.Action, user);

            try
            {
                Powershell.RunPowerShell(psCmd);
                _logger.LogInformation("On-prem membership operation succeeded. Action: {Action}, User: {User}, DL: {DL}", req.Action, user, req.DLName);
            }
            catch (Exception ex)
            {
                string message = ex.Message ?? "";

                if (IsAlreadyMemberError(message))
                {
                    _logger.LogWarning("User {User} is already a member of {DL}. Treating as success.", user, req.DLName);
                    return; // idempotent — not a failure
                }

                if (IsNotMemberError(message))
                {
                    _logger.LogWarning("User {User} is not a member of {DL}. Cannot remove.", user, req.DLName);
                    throw new ServiceException($"User '{user}' is not a member of '{req.DLName}'. Remove operation skipped.", ex);
                }

                if (IsUserNotFoundError(message))
                {
                    _logger.LogError("User {User} not found in Active Directory.", user);
                    throw new ServiceException($"User '{user}' was not found in Active Directory.", ex);
                }

                _logger.LogError(ex, "On-prem membership operation failed. Action: {Action}, User: {User}, DL: {DL}", req.Action, user, req.DLName);
                throw new ServiceException($"Failed to {req.Action.ToLower()} user '{user}' in DL '{req.DLName}'. Details: {ex.Message}", ex);
            }
        }

        private void ExecuteCloudMembership(DL req, string user)
        {
            string template = req.Action == "Add"
                ? _cmd.Membership.Cloud.Add
                : _cmd.Membership.Cloud.Remove;

            string psCmd = template
                .Replace("{DLName}", req.DLName)
                .Replace("{User}", user);

            _logger.LogInformation("Using cloud membership command template. Action: {Action}, User: {User}", req.Action, user);

            try
            {
                // ✅ FIX: Add/Remove-DistributionGroupMember returns NO output on success.
                //         A thrown exception means failure — NOT an empty return value.
                RunExchangeCloudCommand(psCmd);
                _logger.LogInformation("Cloud membership operation succeeded. Action: {Action}, User: {User}, DL: {DL}", req.Action, user, req.DLName);
            }
            catch (ServiceException ex)
            {
                string message = ex.InnerException?.Message ?? ex.Message;

                if (IsAlreadyMemberError(message))
                {
                    _logger.LogWarning("User {User} is already a member of {DL} (cloud). Treating as success.", user, req.DLName);
                    return; // idempotent — not a failure
                }

                if (IsNotMemberError(message))
                {
                    _logger.LogWarning("User {User} is not a member of {DL} (cloud). Cannot remove.", user, req.DLName);
                    throw new ServiceException($"User '{user}' is not a member of '{req.DLName}'. Remove operation skipped.", ex);
                }

                if (IsUserNotFoundError(message))
                {
                    _logger.LogError("User {User} not found in Exchange Online.", user);
                    throw new ServiceException($"User '{user}' was not found in Exchange Online.", ex);
                }

                _logger.LogError(ex, "Cloud membership operation failed. Action: {Action}, User: {User}, DL: {DL}", req.Action, user, req.DLName);
                throw new ServiceException($"Failed to {req.Action.ToLower()} user '{user}' in DL '{req.DLName}' (cloud). Details: {message}", ex);
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // RIGHTS MANAGEMENT
        // ─────────────────────────────────────────────────────────────────────────
        public void RightsManagementCommand(DL req)
        {
            using var scope = _logger.BeginScope(
                "RightsManagement | DL: {DL}, Event: {Event}, Action: {Action}",
                req.DLName, req.Event, req.Action);

            _logger.LogInformation("RightsManagementCommand started.");

            if (string.IsNullOrWhiteSpace(req.DLName) || string.IsNullOrWhiteSpace(req.Rights))
                throw new ServiceException("DLName and Rights are required.");

            switch (req.Event)
            {
                case "Rights | OnPrem":
                    ExecuteOnPremRights(req);
                    break;

                case "Rights | Cloud":
                    ExecuteCloudRights(req);
                    break;

                default:
                    _logger.LogError("Unsupported rights event type: {Event}", req.Event);
                    throw new ServiceException($"Unsupported rights event: '{req.Event}'. Expected 'Rights | OnPrem' or 'Rights | Cloud'.");
            }

            _logger.LogInformation("RightsManagementCommand completed successfully.");
        }

        private void ExecuteOnPremRights(DL req)
        {
            _logger.LogInformation("---- START ExecuteOnPremRights ----");

            _logger.LogInformation("Incoming Request -> DLName: {DLName}, Rights: {Rights}, Action: {Action}",
                req.DLName, req.Rights, req.Action);

            string template = req.Action == "Add"
                ? _cmd.Rights.OnPrem.Add
                : _cmd.Rights.OnPrem.Remove;

            _logger.LogInformation("Template fetched from config: {Template}", template);

            string psCmd = template
                .Replace("{DLName}", req.DLName)
                .Replace("{Rights}", req.Rights);

            _logger.LogInformation("Final PowerShell Command: {Command}", psCmd);

            try
            {
                var startTime = DateTime.UtcNow;

                _logger.LogInformation("Executing PowerShell command...");

                Powershell.RunPowerShell(psCmd);

                var endTime = DateTime.UtcNow;

                _logger.LogInformation("Execution completed successfully in {Duration} ms",
                    (endTime - startTime).TotalMilliseconds);

                _logger.LogInformation("---- END ExecuteOnPremRights SUCCESS ----");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PowerShell execution FAILED");

                _logger.LogError("Failed Command: {Command}", psCmd);

                _logger.LogError("DLName: {DLName}, Rights: {Rights}, Action: {Action}",
                    req.DLName, req.Rights, req.Action);

                _logger.LogInformation("---- END ExecuteOnPremRights FAILURE ----");

                throw new ServiceException(
                    $"Failed to {req.Action.ToLower()} rights on DL '{req.DLName}'. Details: {ex.Message}", ex);
            }
        }
        private void ExecuteCloudRights(DL req)
        {
            string template = req.Action == "Add"
                ? _cmd.Rights.Cloud.Add
                : _cmd.Rights.Cloud.Remove;

            string psCmd = template
                .Replace("{DLName}", req.DLName)
                .Replace("{User}", req.Rights);

            _logger.LogInformation("Executing cloud rights command. Action: {Action}, DL: {DL}", req.Action, req.DLName);

            try
            {
                // ✅ FIX: Same as membership — no output on success, exception = failure.
                RunExchangeCloudCommand(psCmd);
                _logger.LogInformation("Cloud rights operation succeeded. Action: {Action}, DL: {DL}", req.Action, req.DLName);
            }
            catch (ServiceException ex)
            {
                string message = ex.InnerException?.Message ?? ex.Message;
                _logger.LogError(ex, "Cloud rights operation failed. Action: {Action}, DL: {DL}", req.Action, req.DLName);
                throw new ServiceException($"Failed to {req.Action.ToLower()} rights on DL '{req.DLName}' (cloud). Details: {message}", ex);
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // ACCOUNT UNLOCK
        // ─────────────────────────────────────────────────────────────────────────
        public void AccountUnlockCommand(DL req)
        {
            using var scope = _logger.BeginScope("AccountUnlock | EmployeeId: {EmpId}", req.EmpIds);

            if (string.IsNullOrWhiteSpace(req.EmpIds))
                throw new ServiceException("EmployeeId is required.");

            // Step 1: Unlock
            string unlockCmd = _cmd.AccountUnlock.Unlock.Replace("{EmpId}", req.EmpIds);
            _logger.LogInformation("Executing account unlock command for user: {EmpId}", req.EmpIds);

            try
            {
                Powershell.RunPowerShell(unlockCmd);
                _logger.LogInformation("Account unlock command executed for user: {EmpId}", req.EmpIds);
            }
            catch (Exception ex)
            {
                if (IsUserNotFoundError(ex.Message))
                {
                    _logger.LogError("User {EmpId} not found in Active Directory. Cannot unlock.", req.EmpIds);
                    throw new ServiceException($"User '{req.EmpIds}' was not found in Active Directory.", ex);
                }

                _logger.LogError(ex, "Account unlock PowerShell command failed for user: {EmpId}", req.EmpIds);
                throw new ServiceException($"Failed to unlock account for user '{req.EmpIds}'. Details: {ex.Message}", ex);
            }

            // Step 2: Verify
            string verifyCmd = _cmd.AccountUnlock.Verify.Replace("{EmpId}", req.EmpIds);
            _logger.LogInformation("Verifying account unlock status for user: {EmpId}", req.EmpIds);

            string resultJson;
            try
            {
                resultJson = Powershell.RunPowerShell(verifyCmd);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Account unlock verification command failed for user: {EmpId}", req.EmpIds);
                throw new ServiceException($"Could not verify unlock status for user '{req.EmpIds}'. Details: {ex.Message}", ex);
            }

            if (string.IsNullOrWhiteSpace(resultJson))
            {
                _logger.LogWarning("Verification returned empty output for user: {EmpId}. Assuming unlock succeeded.", req.EmpIds);
                return;
            }

            try
            {
                var user = JsonSerializer.Deserialize<JsonElement>(resultJson);

                if (user.TryGetProperty("LockedOut", out var locked) && locked.GetBoolean())
                {
                    _logger.LogError("Account unlock verification failed. User {EmpId} is still locked.", req.EmpIds);
                    throw new ServiceException($"Account unlock failed for user '{req.EmpIds}'. The account is still locked after the operation.");
                }

                _logger.LogInformation("Account unlocked and verified successfully for user: {EmpId}", req.EmpIds);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Could not parse verification JSON for user: {EmpId}. Raw output: {Output}", req.EmpIds, resultJson);
                // Don't fail — unlock command itself succeeded, verification parse is secondary
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // EXCHANGE CLOUD RUNNER
        // ─────────────────────────────────────────────────────────────────────────
        public string RunExchangeCloudCommand(string command)
        {
            try
            {
                string connectScript = _cmd.Membership.Cloud.ConnectScript;

                if (string.IsNullOrWhiteSpace(connectScript))
                    throw new ServiceException("Configuration error: Exchange connect script is missing.");

                _logger.LogInformation("STEP 1 → Connecting to Exchange Online");

                var result = Powershell.RunPowerShellCloudConnect(connectScript, command);

                _logger.LogInformation("Exchange command executed successfully.");
                return result; // may be empty — that is normal for Add/Remove commands
            }
            catch (ServiceException)
            {
                throw; // already wrapped
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exchange Cloud command execution failed.");
                throw new ServiceException(
                    "Exchange Online execution failed. Possible causes: certificate issue, missing app permission, or Exchange role not assigned.",
                    ex);
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // ERROR CLASSIFIERS
        // ─────────────────────────────────────────────────────────────────────────
        private static bool IsAlreadyMemberError(string message) =>
            message.Contains("already a member", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("already exists", StringComparison.OrdinalIgnoreCase);

        private static bool IsNotMemberError(string message) =>
            message.Contains("is not a member", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("not a member", StringComparison.OrdinalIgnoreCase);

        private static bool IsUserNotFoundError(string message) =>
            message.Contains("Cannot find an object", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("couldn't be found", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("recipient", StringComparison.OrdinalIgnoreCase) &&
            message.Contains("not found", StringComparison.OrdinalIgnoreCase);
    }
}