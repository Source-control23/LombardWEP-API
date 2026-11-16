namespace LombardAPI_NEW.Models.DTOs
{
    public class AddOrRemoveRightsDTO
    {
        public string? DLName { get; set; }
        public string? DLOwner { get; set; }
        public string? Action { get; set; }

        public string? Rights { get; set; }
        public string? Source { get; set; }

        // Logged-in user raising the request (sent by the frontend; a validated
        // Entra ID token on the request takes precedence).
        public string? RequestorId { get; set; }
        public string? RequestorName { get; set; }
    }
}
