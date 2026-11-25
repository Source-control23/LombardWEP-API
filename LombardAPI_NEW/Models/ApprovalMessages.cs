namespace LombardAPI_NEW.Models
{
    /// <summary>User-facing messages for the approval workflow (shown as-is by the frontend).</summary>
    public static class ApprovalMessages
    {
        public const string SentToOwner = "This request has been sent to the DL owner for approval via email.";

        public const string Approved = "Approved Successfully";
        public const string Rejected = "Rejected Successfully";

        public const string InvalidLink = "Invalid or expired approval link.";
        public const string InvalidAccess = "Invalid access. This approval link can only be used by the DL owner it was sent to.";
        public const string Processing = "This request is currently being processed. Please refresh in a moment.";
        public const string ApprovalFailed = "Approval failed. The request is still pending; please try again or contact support.";
        public const string ServiceUnavailable = "The request could not be completed right now. Please try again later or contact support.";

        public static string AlreadyProcessed(string status) =>
            $"This request has already been {status.ToLowerInvariant()}. No further action is required.";
    }
}
