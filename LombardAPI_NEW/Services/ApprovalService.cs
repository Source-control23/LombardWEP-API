using LombardAPI_NEW.DataBase;
using LombardAPI_NEW.ErrorHandling;
using LombardAPI_NEW.Interfaces;
using LombardAPI_NEW.Models;
using LombardAPI_NEW.Shared;
using Microsoft.EntityFrameworkCore;

namespace LombardAPI_NEW.Services
{
    public class ApprovalService : IApproval
    {
        private const string StatusPending = "Pending";
        private const string StatusProcessing = "Processing";
        private const string StatusApproved = "Approved";
        private const string StatusRejected = "Rejected";

        private readonly DataBaseContext _context;
        private readonly PowershellCommandService _powershellcommand;
        private readonly ILogger<ApprovalService> _logger;
        private readonly IMembershipNotificationService _notificationService;

        public ApprovalService(DataBaseContext context, PowershellCommandService powershellCommand, ILogger<ApprovalService> logger,
            IMembershipNotificationService notificationService)
        {
            _context = context;
            _powershellcommand = powershellCommand;
            _logger = logger;
            _notificationService = notificationService;
        }

        /// <summary>Pending request by ApprovalID (kept for existing callers).</summary>
        public async Task<DL?> GetRequestasync(string id)
        {
            try
            {
                return await _context.DLDetails
                    .Where(d => d.ApprovalID == id && d.ApprovalStatus == StatusPending)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load pending request.");
                return null;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // VIEW (approval page)
        // ─────────────────────────────────────────────────────────────────────
        public async Task<ApprovalOutcome> GetRequestForApproverAsync(string id, CallerIdentity? caller)
        {
            var (request, denied) = await LoadAuthorisedAsync(id, caller);
            if (denied != null)
                return denied;

            // Any status is returned so the page can say "already approved/rejected".
            return ApprovalOutcome.Ok("Success", request);
        }

        // ─────────────────────────────────────────────────────────────────────
        // APPROVE
        // ─────────────────────────────────────────────────────────────────────
        public async Task<ApprovalOutcome> ApproveAsync(string id, CallerIdentity? caller)
        {
            var (request, denied) = await LoadAuthorisedAsync(id, caller);
            if (denied != null)
                return denied;

            if (request!.ApprovalStatus != StatusPending)
                return StatusConflict(request.ApprovalStatus);

            // Atomically claim the request so a double click / two owners cannot execute it twice.
            int claimed = await _context.DLDetails
                .Where(d => d.ApprovalID == request.ApprovalID && d.ApprovalStatus == StatusPending)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.ApprovalStatus, StatusProcessing));

            if (claimed == 0)
                return StatusConflict(await CurrentStatusAsync(request.ApprovalID));

            _logger.LogInformation("Approval started. ApprovalID: {ApprovalID}, Event: {Event}", request.ApprovalID, request.Event);

            IReadOnlyList<MembershipExecutionResult>? results;
            try
            {
                results = ExecuteRequest(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Execution failed. ApprovalID: {ApprovalID}", request.ApprovalID);

                // Release the claim so the owner can retry (same as the previous behaviour, where the
                // request stayed Pending when execution failed).
                await _context.DLDetails
                    .Where(d => d.ApprovalID == request.ApprovalID && d.ApprovalStatus == StatusProcessing)
                    .ExecuteUpdateAsync(s => s.SetProperty(d => d.ApprovalStatus, StatusPending));

                // Improper Error Handling (DAST): never return raw PowerShell / AD / Exchange output
                // to the browser. Business messages are kept ("User 'x' was not found in Active
                // Directory."), but anything after " Details:" (the raw engine error) is logged only.
                string message = ex is ServiceException
                    ? $"Approval failed: {ToClientSafeMessage(ex.Message)} The request is still pending."
                    : ApprovalMessages.ApprovalFailed;

                return ApprovalOutcome.Failed(message);
            }

            DateTime approvedAt = DateTime.UtcNow;
            await _context.DLDetails
                .Where(d => d.ApprovalID == request.ApprovalID)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(d => d.ApprovalStatus, StatusApproved)
                    .SetProperty(d => d.ApprovalTime, approvedAt));

            request.ApprovalStatus = StatusApproved;
            request.ApprovalTime = approvedAt;

            _logger.LogInformation("Approval completed successfully. ApprovalID: {ApprovalID}", request.ApprovalID);

            // Completion email. The change is already executed and persisted, so a mail failure is
            // logged but does not fail the approval.
            if (results != null)
            {
                await TryNotifyAsync(() => _notificationService.SendCompletionAsync(request, results, caller!.DisplayName),
                    "Completion", request.ApprovalID);
            }

            return ApprovalOutcome.Ok(ApprovalMessages.Approved, request);
        }

        // ─────────────────────────────────────────────────────────────────────
        // REJECT
        // ─────────────────────────────────────────────────────────────────────
        public async Task<ApprovalOutcome> RejectAsync(string id, CallerIdentity? caller)
        {
            var (request, denied) = await LoadAuthorisedAsync(id, caller);
            if (denied != null)
                return denied;

            if (request!.ApprovalStatus != StatusPending)
                return StatusConflict(request.ApprovalStatus);

            DateTime rejectedAt = DateTime.UtcNow;
            int updated = await _context.DLDetails
                .Where(d => d.ApprovalID == request.ApprovalID && d.ApprovalStatus == StatusPending)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(d => d.ApprovalStatus, StatusRejected)
                    .SetProperty(d => d.ApprovalTime, rejectedAt));

            if (updated == 0)
                return StatusConflict(await CurrentStatusAsync(request.ApprovalID));

            request.ApprovalStatus = StatusRejected;
            request.ApprovalTime = rejectedAt;

            _logger.LogInformation("Request rejected. ApprovalID: {ApprovalID}", request.ApprovalID);

            if (IsMembershipOrRights(request))
            {
                await TryNotifyAsync(() => _notificationService.SendRejectionAsync(request, caller!.DisplayName),
                    "Rejection", request.ApprovalID);
            }

            return ApprovalOutcome.Ok(ApprovalMessages.Rejected, request);
        }

        // ─────────────────────────────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Loads the request and checks that the signed-in caller is one of its approvers
        /// (the DL owner the approval email was sent to). Forwarded links fail here.
        /// </summary>
        private async Task<(DL? Request, ApprovalOutcome? Denied)> LoadAuthorisedAsync(string id, CallerIdentity? caller)
        {
            if (caller == null)
                return (null, ApprovalOutcome.Unauthenticated());

            // Approval IDs are GUIDs; anything else is rejected before touching the DB.
            if (!Guid.TryParse(id, out var guid))
                return (null, ApprovalOutcome.NotFound());

            string approvalId = guid.ToString();

            DL? request = await _context.DLDetails
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.ApprovalID == approvalId);

            if (request == null || !IsMembershipOrRights(request))
                return (null, ApprovalOutcome.NotFound());

            var approvers = _notificationService.GetApproverEmails(request);
            if (approvers.Count == 0 || !caller.MatchesAny(approvers))
            {
                _logger.LogWarning("Approval access denied. ApprovalID: {ApprovalID}, Caller: {Caller}",
                    approvalId, RequestValidation.ForLog(caller.PrimaryId));
                return (null, ApprovalOutcome.Forbidden());
            }

            return (request, null);
        }

        private IReadOnlyList<MembershipExecutionResult>? ExecuteRequest(DL req)
        {
            if (req.Event == "Membership | OnPrem" || req.Event == "Membership | Cloud")
            {
                return _powershellcommand.GroupMembershipCommand(req);
            }

            if (req.Event == "Rights | OnPrem" || req.Event == "Rights | Cloud")
            {
                _powershellcommand.RightsManagementCommand(req);   // throws on failure
                return MembershipNotificationService.GetRequestedIds(req)
                    .Select(rid => new MembershipExecutionResult(rid, MembershipExecutionResult.Success))
                    .ToList();
            }

            throw new ServiceException($"Unsupported request type '{req.Event}'.");
        }

        /// <summary>Drops the raw engine error appended after " Details:" and any control characters.</summary>
        private static string ToClientSafeMessage(string? message)
        {
            string value = message ?? string.Empty;
            int detailsAt = value.IndexOf(" Details:", StringComparison.Ordinal);
            if (detailsAt >= 0)
                value = value.Substring(0, detailsAt);

            return RequestValidation.ForLog(value).Trim();
        }

        private static bool IsMembershipOrRights(DL req) =>
            req.Event is "Membership | OnPrem" or "Membership | Cloud" or "Rights | OnPrem" or "Rights | Cloud";

        private async Task<string> CurrentStatusAsync(string approvalId) =>
            await _context.DLDetails.AsNoTracking()
                .Where(d => d.ApprovalID == approvalId)
                .Select(d => d.ApprovalStatus)
                .FirstOrDefaultAsync() ?? StatusPending;

        private static ApprovalOutcome StatusConflict(string status) =>
            status == StatusProcessing
                ? ApprovalOutcome.Conflict(ApprovalMessages.Processing)
                : ApprovalOutcome.Conflict(ApprovalMessages.AlreadyProcessed(status));

        private async Task TryNotifyAsync(Func<Task<bool>> send, string kind, string approvalId)
        {
            try
            {
                if (!await send())
                    _logger.LogWarning("{Kind} email was not sent. ApprovalID: {ApprovalID}", kind, approvalId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Kind} email failed. ApprovalID: {ApprovalID}", kind, approvalId);
            }
        }
    }
}
