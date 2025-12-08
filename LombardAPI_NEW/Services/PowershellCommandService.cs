using LombardAPI_NEW.Models;
using LombardAPI_NEW.Shared;

namespace LombardAPI_NEW.Services
{
    public class PowershellCommandService
    {
        public void GroupMembershipCommand(DL req)
        {

            string psCmd = req.Action == "Add"
               ? $"Add-ADGroupMember -Identity \"{req.DLName}\" -Members \"{req.EmpIds}\""
               : $"Remove-ADGroupMember -Identity \"{req.DLName}\" -Members \"{req.EmpIds}\" -Confirm:$false";

            Powershell.RunPowerShell(psCmd);
        }

        


        public void RightsManagementCommand(DL req)
        {
            string psCmd = req.Action == "Add"
               ? $"Set-DistributionGroup -Identity \"{req.DLName}\" -AcceptMessagesOnlyFrom @{{Add=\"{req.Rights}\""
               : $"Set-DistributionGroup -Identity \"{req.DLName}\" -AcceptMessagesOnlyFrom @{{Remove=\"{req.Rights}\"";

            Powershell.RunPowerShell(psCmd);
        }

        public void AccountUnlockCommand(DL req)
        {
            string psCmd = "";

            Powershell.RunPowerShell(psCmd);
        }

    }
}
