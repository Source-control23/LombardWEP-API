using LombardAPI_NEW.Models;
using LombardAPI_NEW.Shared;
using System.Net;
using System.Text;

namespace LombardAPI_NEW.Services
{
    /// <summary>
    /// Emails for the DL approval workflow (DL Membership and DL Rights / Send Email access).
    /// All recipients are resolved dynamically; every dynamic value is HTML-encoded.
    /// </summary>
    public interface IMembershipNotificationService
    {
        /// <summary>Approval email to the DL owner(s) stored as approvers on the request.</summary>
        Task<bool> SendApprovalRequestAsync(DL request);

        /// <summary>Completion email to the requestor (requested IDs in CC) after approval and execution.</summary>
        Task<bool> SendCompletionAsync(DL request, IReadOnlyList<MembershipExecutionResult> results, string? approvedBy = null);

        /// <summary>Rejection email to the requestor (requested IDs in CC).</summary>
        Task<bool> SendRejectionAsync(DL request, string? rejectedBy = null);

        /// <summary>Email addresses allowed to approve/reject the request (the DL owner(s)).</summary>
        List<string> GetApproverEmails(DL request);
    }

    public class MembershipNotificationService : IMembershipNotificationService
    {
        private readonly IEmailService _emailService;
        private readonly EmailRecipientResolver _recipients;
        private readonly CommonService _commonService;
        private readonly ILogger<MembershipNotificationService> _logger;

        public MembershipNotificationService(
            IEmailService emailService,
            EmailRecipientResolver recipients,
            CommonService commonService,
            ILogger<MembershipNotificationService> logger)
        {
            _emailService = emailService;
            _recipients = recipients;
            _commonService = commonService;
            _logger = logger;
        }

        public static bool IsRightsRequest(DL request) =>
            request.Event == "Rights | OnPrem" || request.Event == "Rights | Cloud";

        /// <summary>IDs the request is about: EmpIds for membership, Rights for Send Email access.</summary>
        public static List<string> GetRequestedIds(DL request) =>
            EmailRecipientResolver.SplitEmployeeIds(IsRightsRequest(request) ? request.Rights : request.EmpIds);

        /// <summary>Approvers stored on the request; falls back to the owner string for older rows.</summary>
        public List<string> GetApproverEmails(DL request)
        {
            var stored = (request.ApproverEmail ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            return stored.Count > 0 ? stored : _recipients.ResolveOwnerEmails(request.DLOWner);
        }

        // ─────────────────────────────────────────────────────────────────────
        // APPROVAL REQUEST → DL OWNER
        // ─────────────────────────────────────────────────────────────────────
        public async Task<bool> SendApprovalRequestAsync(DL request)
        {
            var ownerEmails = GetApproverEmails(request);
            if (ownerEmails.Count == 0)
            {
                _logger.LogError("Could not resolve DL owner email for ApprovalID {ApprovalId}.", request.ApprovalID);
                return false;
            }

            var ids = GetRequestedIds(request);
            bool isAdd = IsAdd(request.Action);
            bool isRights = IsRightsRequest(request);

            string ownerFirstNames = Enc(EmailRecipientResolver.GetOwnerFirstNames(request.DLOWner));
            string requestorName = Enc(FirstNonEmpty(request.RequestorName, request.RequestorId));
            string idList = Enc(string.Join(", ", ids));
            string idLabel = ids.Count == 1 ? "ID" : "IDs";
            string dlName = Enc(request.DLName);
            string link = Enc($"{_commonService.GetEmailBaseUrl()}approval?id={Uri.EscapeDataString(request.ApprovalID)}");

            string requestSentence = isRights
                ? $"{requestorName} has requested to {(isAdd ? "grant" : "remove")} <strong>Send Email access</strong> on the DL <strong>{dlName}</strong> for the {idLabel} <strong>{idList}</strong>."
                : $"{requestorName} has requested the {(isAdd ? "Addition" : "Removal")} of {idLabel} <strong>{idList}</strong> {(isAdd ? "in" : "from")} the DL <strong>{dlName}</strong>.";

            string body = Wrap($@"
      <p>Dear {ownerFirstNames},</p>

      <p>{requestSentence}</p>

      <p>
          As you are the owner of the DL, please approve or reject the request by clicking on the link below.
      </p>

      <p style='margin:20px 0;'>
          <a href='{link}'
             style='background-color:#0052cc;
                    color:#fff;
                    padding:10px 18px;
                    text-decoration:none;
                    border-radius:6px;
                    font-weight:600;'>
              Open Approval Page
          </a>
      </p>

      <p>
          If the button above does not work, please copy and paste the link below into your browser:
      </p>

      <p style='word-break:break-all; color:#0052cc;'>
          {link}
      </p>

      <p style='font-size:13px; color:#555;'>
          Note: This approval link is meant only for you. You will be asked to sign in, and the link
          will not work for anyone else, even if this email is forwarded.
      </p>

      <p>Thank you for your cooperation.</p>");

            string subject = isRights
                ? $"DL Rights Approval - {request.DLName}"
                : $"DL Membership Approval - {request.DLName}";

            return await _emailService.SendEmailAsync(ownerEmails, null, subject, body);
        }

        // ─────────────────────────────────────────────────────────────────────
        // COMPLETION (APPROVED + EXECUTED) → REQUESTOR, CC REQUESTED IDS
        // ─────────────────────────────────────────────────────────────────────
        public async Task<bool> SendCompletionAsync(DL request, IReadOnlyList<MembershipExecutionResult> results, string? approvedBy = null)
        {
            if (!TryGetRequestorRecipients(request, out var to, out var cc))
                return false;

            var ids = GetRequestedIds(request);
            bool isAdd = IsAdd(request.Action);
            bool isRights = IsRightsRequest(request);

            string requestorGreeting = Enc(request.RequestorId);
            string idList = Enc(string.Join(", ", ids));
            string dlName = Enc(request.DLName);
            string approver = Enc(FirstNonEmpty(approvedBy, EmailRecipientResolver.GetOwnerDisplayName(request.DLOWner)));

            var statusRows = new StringBuilder();
            foreach (var r in results)
            {
                statusRows.Append("<li>")
                          .Append(Enc(r.EmployeeId))
                          .Append(": <strong>")
                          .Append(Enc(r.Status))
                          .Append("</strong></li>");
            }

            string paragraphs = isRights
                ? $@"
      <p>
          Kindly note that your request to {(isAdd ? "grant" : "remove")} Send Email access on the DL <strong>{dlName}</strong>
          for the following ID(s), <strong>{idList}</strong>, has been completed.
      </p>

      <p>
          Send Email access has been successfully {(isAdd ? "granted" : "removed")} for the requested ID(s) on
          <strong>{dlName}</strong> as per the approval provided by {approver}.
      </p>"
                : $@"
      <p>
          Kindly note that your request for the {(isAdd ? "Addition" : "Removal")} of the following ID(s), <strong>{idList}</strong>, has been completed.
      </p>

      <p>
          The requested ID(s) have been successfully {(isAdd ? "added to" : "removed from")} <strong>{dlName}</strong> as per the approval provided by {approver}.
      </p>";

            string body = Wrap($@"
      <p>Dear {requestorGreeting},</p>
      {paragraphs}

      <p><strong>Execution Status:</strong></p>
      <ul style='margin-top:0;'>
          {statusRows}
      </ul>");

            string subject = isRights
                ? $"DL Rights Request Completed - {request.DLName}"
                : $"DL Membership Request Completed - {request.DLName}";

            return await _emailService.SendEmailAsync(to, cc, subject, body);
        }

        // ─────────────────────────────────────────────────────────────────────
        // REJECTION → REQUESTOR, CC REQUESTED IDS
        // ─────────────────────────────────────────────────────────────────────
        public async Task<bool> SendRejectionAsync(DL request, string? rejectedBy = null)
        {
            if (!TryGetRequestorRecipients(request, out var to, out var cc))
                return false;

            var ids = GetRequestedIds(request);
            bool isAdd = IsAdd(request.Action);
            bool isRights = IsRightsRequest(request);

            string requestorGreeting = Enc(request.RequestorId);
            string idList = Enc(string.Join(", ", ids));
            string dlName = Enc(request.DLName);
            string rejecter = Enc(FirstNonEmpty(rejectedBy, EmailRecipientResolver.GetOwnerDisplayName(request.DLOWner)));

            string requestText = isRights
                ? $"your request to {(isAdd ? "grant" : "remove")} Send Email access on the DL <strong>{dlName}</strong> for the following ID(s), <strong>{idList}</strong>,"
                : $"your request for the {(isAdd ? "Addition" : "Removal")} of the following ID(s), <strong>{idList}</strong>, {(isAdd ? "to" : "from")} the DL <strong>{dlName}</strong>";

            string body = Wrap($@"
      <p>Dear {requestorGreeting},</p>

      <p>
          Kindly note that {requestText} has been <strong>rejected</strong> by {rejecter} (DL Owner).
      </p>

      <p>
          No changes have been made to the DL. For any clarification, please contact the DL owner.
      </p>

      <p><strong>Request Status:</strong> REJECTED</p>");

            string subject = isRights
                ? $"DL Rights Request Rejected - {request.DLName}"
                : $"DL Membership Request Rejected - {request.DLName}";

            return await _emailService.SendEmailAsync(to, cc, subject, body);
        }

        // ─────────────────────────────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────────────────────────────
        private bool TryGetRequestorRecipients(DL request, out List<string> to, out List<string> cc)
        {
            to = new List<string>();
            cc = new List<string>();

            string? requestorEmail = _recipients.BuildEmailFromEmployeeId(request.RequestorId);
            if (requestorEmail == null)
            {
                // Requests raised before requestor capture was introduced have no RequestorId.
                _logger.LogWarning("Requestor email unavailable for ApprovalID {ApprovalId}. Notification skipped.", request.ApprovalID);
                return false;
            }

            to.Add(requestorEmail);
            cc = GetRequestedIds(request)
                .Select(_recipients.BuildEmailFromEmployeeId)
                .Where(e => e != null)
                .Select(e => e!)
                .ToList();
            return true;
        }

        private static string Wrap(string inner) => $@"
  <div style='font-family:Segoe UI,Arial,sans-serif; font-size:14px; color:#333; line-height:1.6;'>
{inner}

      <hr style='border:none; border-top:1px solid #e0e0e0; margin:25px 0;' />

      <p style='font-size:12px; color:#777;'>
          This is an automated notification. Please do not reply to this email.
      </p>

  </div>";

        // Matches PowershellCommandService semantics: "Add" adds, anything else removes.
        private static bool IsAdd(string? action) => string.Equals(action, "Add", StringComparison.Ordinal);

        private static string Enc(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

        private static string FirstNonEmpty(params string?[] values) =>
            values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? string.Empty;
    }
}
