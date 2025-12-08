namespace LombardAPI_NEW.Models.DTOs
{
    public class AccountUnlockRequestDTO
    {
        public string? EmployeeId { get; set; }
        public string? DLName { get; set; }
        public string? DLOwner { get; set; }
        public bool Unlock { get; set; }
    }
}
