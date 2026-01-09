using LombardAPI_NEW.Models;
using LombardAPI_NEW.Shared;
using System.Text.Json;

namespace LombardAPI_NEW.Services
{
    public class PowershellCommandService
    {
        public void GroupMembershipCommand(DL req)
        {
            if (string.IsNullOrWhiteSpace(req.DLName) || string.IsNullOrWhiteSpace(req.EmpIds))
                throw new ArgumentException("DLName and EmpIds are required.");

            var users = req.EmpIds.Split(',', StringSplitOptions.RemoveEmptyEntries);

            foreach (var user in users)
            {
                string psCmd = "";

                if (req.Event == "Membership | OnPrem")
                {
                    psCmd = req.Action == "Add"
                        ? $"Add-ADGroupMember -Identity \"{req.DLName}\" -Members \"{user.Trim()}\""
                        : $"Remove-ADGroupMember -Identity \"{req.DLName}\" -Members \"{user.Trim()}\" -Confirm:$false";
                }
                else if (req.Event == "Membership | Cloud")
                {
                    psCmd = req.Action == "Add"
                        ? $"Add-DistributionGroupMember -Identity \"{req.DLName}\" -Member \"{user.Trim()}@icicilombard.com\" -Confirm:$false"
                        : $"Remove-DistributionGroupMember -Identity \"{req.DLName}\" -Member \"{user.Trim()}@icicilombard.com\" -Confirm:$false";
                }

                Powershell.RunPowerShell(psCmd);
            }
        }



        public void RightsManagementCommand(DL req)
        {
            if (string.IsNullOrWhiteSpace(req.DLName) || string.IsNullOrWhiteSpace(req.Rights))
                throw new ArgumentException("DLName and Rights are required.");

            string psCmd = "";

            if (req.Event == "Rights | OnPrem")
            {
                psCmd = req.Action == "Add"
                    ? $"Set-DistributionGroup -Identity \"{req.DLName}\" -AcceptMessagesOnlyFrom @{{Add=\"{req.Rights}\"}}"
                    : $"Set-DistributionGroup -Identity \"{req.DLName}\" -AcceptMessagesOnlyFrom @{{Remove=\"{req.Rights}\"}}";
            }
            else if (req.Event == "Rights | Cloud")
            {
                psCmd = req.Action == "Add"
                    ? $@"
$group = Get-AzureADGroup -SearchString ""{req.DLName}""
$user  = Get-AzureADUser -ObjectId ""{req.Rights}""
Add-AzureADGroupMember -ObjectId $group.ObjectId -RefObjectId $user.ObjectId"
                    : $@"
$group = Get-AzureADGroup -SearchString ""{req.DLName}""
$user  = Get-AzureADUser -ObjectId ""{req.Rights}""
Remove-AzureADGroupMember -ObjectId $group.ObjectId -MemberId $user.ObjectId";
            }

            Powershell.RunPowerShell(psCmd);
        }


        public void AccountUnlockCommand(DL req)
        {
            if (string.IsNullOrWhiteSpace(req.EmpIds))
                throw new ArgumentException("EmployeeId is required for account unlock.");

            string unlockCmd = $"Unlock-ADAccount -Identity \"{req.EmpIds}\"";
            Powershell.RunPowerShell(unlockCmd);

            string verifyCmd = $@"
Get-ADUser -Identity ""{req.EmpIds}"" -Properties LockedOut |
Select SamAccountName, LockedOut | ConvertTo-Json";

            var resultJson = Powershell.RunPowerShell(verifyCmd);

            if (!string.IsNullOrWhiteSpace(resultJson))
            {
                var user = JsonSerializer.Deserialize<JsonElement>(resultJson);
                bool isLocked = user.GetProperty("LockedOut").GetBoolean();

                if (isLocked)
                    throw new Exception("Account unlock failed. User is still locked.");
            }
        }


    }
}
