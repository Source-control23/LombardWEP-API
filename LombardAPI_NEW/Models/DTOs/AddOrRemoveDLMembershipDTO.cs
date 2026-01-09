namespace LombardAPI_NEW.Models.DTOs
{
    public class AddOrRemoveDLMembershipDTO
    {
        public string? DLName { get; set; }
        public string? DLOwner { get; set; }
        public string? Action { get; set; }
        public string? EmpIds { get; set; }
        public string? Source { get; set; }

    }
}
