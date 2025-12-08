namespace LombardAPI_NEW.Models.DTOs
{
    public class AccountUnlockResponseDTO
    {
        public string? EmployeeId { get; set; }
        public bool IsLocked { get; set; }
        public string? Message { get; set; }
        public List<DL>? UserDetails { get; set; }
    }
}
