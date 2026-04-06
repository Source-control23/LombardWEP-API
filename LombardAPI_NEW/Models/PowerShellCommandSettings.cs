namespace LombardAPI_NEW.Models
{
    public class PowerShellCommandSettings
    {
        public MembershipCommands Membership { get; set; }
        public RightsCommands Rights { get; set; }
        public AccountUnlockCommands AccountUnlock { get; set; }
        public GALCommands GAL { get; set; }
    }

    public class GALCommands
    {
        public string EmpId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Mail { get; set; }
        public string DisplayName { get; set; }
        public List<string> SearchBaseOUs { get; set; } = new();
    }
    public class MembershipCommands
    {
        public EnvironmentCommands OnPrem { get; set; }
        public EnvironmentCommands Cloud { get; set; }
    }

    public class RightsCommands
    {
        public EnvironmentCommands OnPrem { get; set; }
        public EnvironmentCommands Cloud { get; set; }
    }

    public class EnvironmentCommands
    {
        public string ConnectScript { get; set; }
        public string Get { get; set; }
        public string GetDLWithOwner { get; set; }
        public string ManagedBy { get; set; }
        public string Add { get; set; }
        public string Remove { get; set; }
    }

    public class AccountUnlockCommands
    {
        public string Unlock { get; set; }
        public string Verify { get; set; }
    }

}
