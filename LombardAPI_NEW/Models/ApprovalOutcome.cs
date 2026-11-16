namespace LombardAPI_NEW.Models
{
    public enum ApprovalOutcomeStatus
    {
        Success,
        Unauthenticated,
        NotFound,
        Forbidden,
        Conflict,
        Failed
    }

    /// <summary>Result of an approval-page action, mapped to an HTTP status by the controller.</summary>
    public sealed record ApprovalOutcome(ApprovalOutcomeStatus Status, string Message, DL? Request = null)
    {
        public static ApprovalOutcome Ok(string message, DL? request) => new(ApprovalOutcomeStatus.Success, message, request);
        public static ApprovalOutcome Unauthenticated() => new(ApprovalOutcomeStatus.Unauthenticated, "Please sign in to continue.");
        public static ApprovalOutcome NotFound() => new(ApprovalOutcomeStatus.NotFound, ApprovalMessages.InvalidLink);
        public static ApprovalOutcome Forbidden() => new(ApprovalOutcomeStatus.Forbidden, ApprovalMessages.InvalidAccess);
        public static ApprovalOutcome Conflict(string message) => new(ApprovalOutcomeStatus.Conflict, message);
        public static ApprovalOutcome Failed(string message) => new(ApprovalOutcomeStatus.Failed, message);
    }
}
