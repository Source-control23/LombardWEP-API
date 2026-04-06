namespace LombardAPI_NEW.Models.DTOs
{
    public class AccountUnlockResponseDTO
    {
        public string EmployeeId { get; set; } = string.Empty;
        public bool IsLocked { get; set; }
        public string AccountStatus { get; set; } = string.Empty;
        public string AccountExpiry { get; set; } = string.Empty;
        public string PasswordStatus { get; set; } = string.Empty;
        public string Info { get; set; } = string.Empty;
    }


}
