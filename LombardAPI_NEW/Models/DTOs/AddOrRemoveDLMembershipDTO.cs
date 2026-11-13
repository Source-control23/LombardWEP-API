namespace LombardAPI_NEW.Models.DTOs
{
    public class AddOrRemoveDLMembershipDTO
    {
        public string? DLName { get; set; }
        public string? DLOwner { get; set; }
        public string? Action { get; set; }
        public string? EmpIds { get; set; }
        public string? Source { get; set; }
        public string? SendMailAccess { get; set; }
        public string? GroupOwnershipChange { get; set; }

        // Logged-in user raising the request (sent by the frontend).
        // RequestorId = employee / NT ID (e.g. "lomad"), used to build the requestor's email.
        // RequestorName = display name (e.g. "ADSUPPORT /IT/ICICILOMBARD/PRBD"), used in the mail body.
        public string? RequestorId { get; set; }
        public string? RequestorName { get; set; }
    }
}
