namespace LombardAPI_NEW.Models
{
    /// <summary>Per-user outcome of a DL membership Add/Remove execution.</summary>
    public sealed record MembershipExecutionResult(string EmployeeId, string Status)
    {
        public const string Success = "SUCCESS";
    }
}
