using LombardAPI_NEW.Models;
using LombardAPI_NEW.Shared;

namespace LombardAPI_NEW.Interfaces
{
    public interface IApproval
    {
        Task<DL?> GetRequestasync(string id);

        /// <summary>Request details, only for one of the request's approvers.</summary>
        Task<ApprovalOutcome> GetRequestForApproverAsync(string id, CallerIdentity? caller);

        /// <summary>Approve + execute, only for one of the request's approvers.</summary>
        Task<ApprovalOutcome> ApproveAsync(string id, CallerIdentity? caller);

        /// <summary>Reject, only for one of the request's approvers.</summary>
        Task<ApprovalOutcome> RejectAsync(string id, CallerIdentity? caller);
    }
}
