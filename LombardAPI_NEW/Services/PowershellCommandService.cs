using LombardAPI_NEW.Models;
using LombardAPI_NEW.Shared;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace LombardAPI_NEW.Services
{
  public class PowershellCommandService
    {
        private readonly PowerShellCommandSettings _cmd;

        public PowershellCommandService(IOptions<PowerShellCommandSettings> options)
        {
            _cmd = options.Value;
        }

        public void GroupMembershipCommand(DL req)
        {
            if (string.IsNullOrWhiteSpace(req.DLName) || string.IsNullOrWhiteSpace(req.EmpIds))
                throw new ArgumentException("DLName and EmpIds are required.");

            var users = req.EmpIds.Split(',', StringSplitOptions.RemoveEmptyEntries);

            foreach (var user in users)
            {
                string template = req.Event switch
                {
                    "Membership | OnPrem" => req.Action == "Add"
                        ? _cmd.Membership.OnPrem.Add
                        : _cmd.Membership.OnPrem.Remove,

                    "Membership | Cloud" => req.Action == "Add"
                        ? _cmd.Membership.Cloud.Add
                        : _cmd.Membership.Cloud.Remove,

                    _ => throw new InvalidOperationException("Invalid Membership Event")
                };

                string psCmd = template
                    .Replace("{DLName}", req.DLName)
                    .Replace("{User}", user.Trim());

                Powershell.RunPowerShell(psCmd);
            }
        }

        public void RightsManagementCommand(DL req)
        {
            if (string.IsNullOrWhiteSpace(req.DLName) || string.IsNullOrWhiteSpace(req.Rights))
                throw new ArgumentException("DLName and Rights are required.");

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

            Powershell.RunPowerShell(psCmd);
        }

        public void AccountUnlockCommand(DL req)
        {
            if (string.IsNullOrWhiteSpace(req.EmpIds))
                throw new ArgumentException("EmployeeId is required.");

            string unlockCmd = _cmd.AccountUnlock.Unlock
                .Replace("{EmpId}", req.EmpIds);

            Powershell.RunPowerShell(unlockCmd);

            string verifyCmd = _cmd.AccountUnlock.Verify
                .Replace("{EmpId}", req.EmpIds);

            var resultJson = Powershell.RunPowerShell(verifyCmd);

            if (!string.IsNullOrWhiteSpace(resultJson))
            {
                var user = JsonSerializer.Deserialize<JsonElement>(resultJson);
                if (user.GetProperty("LockedOut").GetBoolean())
                    throw new Exception("Account unlock failed. User is still locked.");
            }
        }
    }

}
