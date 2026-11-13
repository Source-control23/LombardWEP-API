using LombardAPI_NEW.Models;
using LombardAPI_NEW.Shared;
using System.Net;
using System.Text;

namespace LombardAPI_NEW.Services
{
    public interface IMembershipNotificationService
    {
        /// <summary>Approval email to the DL owner(s), resolved dynamically from the stored DL owner.</summary>
        Task<bool> SendApprovalRequestAsync(DL request);

        /// <summary>Completion email to the requestor, with the requested ID(s) in CC.</summary>
        Task<bool> SendCompletionAsync(DL request, IReadOnlyList<MembershipExecutionResult> results);
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

        public async Task<bool> SendApprovalRequestAsync(DL request)
        {
            var ownerEmails = _recipients.ResolveOwnerEmails(request.DLOWner);
            if (ownerEmails.Count == 0)
            {
                _logger.LogError("Could not resolve DL owner email for ApprovalID {ApprovalId}.", request.ApprovalID);
                return false;
            }

            var empIds = EmailRecipientResolver.SplitEmployeeIds(request.EmpIds);
            bool isAdd = IsAdd(request.Action);

            string ownerFirstNames = Enc(EmailRecipientResolver.GetOwnerFirstNames(request.DLOWner));
            string requestorName = Enc(FirstNonEmpty(request.RequestorName, request.RequestorId));
            string actionNoun = isAdd ? "Addition" : "Removal";
            string preposition = isAdd ? "in" : "from";
            string idLabel = empIds.Count == 1 ? "ID" : "IDs";
            string ids = Enc(string.Join(", ", empIds));
            string dlName = Enc(request.DLName);
            string link = Enc($"{_commonService.GetEmailBaseUrl()}approval?id={Uri.EscapeDataString(request.ApprovalID)}");

            string body = $@"
  <div style='font-family:Segoe UI,Arial,sans-serif; font-size:14px; color:#333; line-height:1.6;'>

      <p>Dear {ownerFirstNames},</p>

      <p>
          {requestorName} has requested the {actionNoun} of {idLabel} <strong>{ids}</strong> {preposition} the DL <strong>{dlName}</strong>.
      </p>

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

      <p>Thank you for your cooperation.</p>

      <hr style='border:none; border-top:1px solid #e0e0e0; margin:25px 0;' />

      <p style='font-size:12px; color:#777;'>
          This is an automated notification. Please do not reply to this email.
      </p>

  </div>";

            return await _emailService.SendEmailAsync(
                ownerEmails,
                null,
                $"DL Membership Approval - {request.DLName}",
                body);
        }

        public async Task<bool> SendCompletionAsync(DL request, IReadOnlyList<MembershipExecutionResult> results)
        {
            string? requestorEmail = _recipients.BuildEmailFromEmployeeId(request.RequestorId);
            if (requestorEmail == null)
            {
                // Requests raised before requestor capture was introduced have no RequestorId.
                _logger.LogWarning("Requestor email unavailable for ApprovalID {ApprovalId}. Completion email skipped.", request.ApprovalID);
                return false;
            }

            var empIds = EmailRecipientResolver.SplitEmployeeIds(request.EmpIds);

            var ccEmails = empIds
                .Select(_recipients.BuildEmailFromEmployeeId)
                .Where(e => e != null)
                .Select(e => e!)
                .ToList();

            bool isAdd = IsAdd(request.Action);

            string requestorGreeting = Enc(request.RequestorId);
            string actionNoun = isAdd ? "Addition" : "Removal";
            string actionVerb = isAdd ? "added to" : "removed from";
            string ids = Enc(string.Join(", ", empIds));
            string dlName = Enc(request.DLName);
            string ownerName = Enc(EmailRecipientResolver.GetOwnerDisplayName(request.DLOWner));

            var statusRows = new StringBuilder();
            foreach (var r in results)
            {
                statusRows.Append("<li>")
                          .Append(Enc(r.EmployeeId))
                          .Append(": <strong>")
                          .Append(Enc(r.Status))
                          .Append("</strong></li>");
            }

            string body = $@"
  <div style='font-family:Segoe UI,Arial,sans-serif; font-size:14px; color:#333; line-height:1.6;'>

      <p>Dear {requestorGreeting},</p>

      <p>
          Kindly note that your request for the {actionNoun} of the following ID(s), <strong>{ids}</strong>, has been completed.
      </p>

      <p>
          The requested ID(s) have been successfully {actionVerb} <strong>{dlName}</strong> as per the approval provided by {ownerName}.
      </p>

      <p><strong>Execution Status:</strong></p>
      <ul style='margin-top:0;'>
          {statusRows}
      </ul>

      <hr style='border:none; border-top:1px solid #e0e0e0; margin:25px 0;' />

      <p style='font-size:12px; color:#777;'>
          This is an automated notification. Please do not reply to this email.
      </p>

  </div>";

            return await _emailService.SendEmailAsync(
                new[] { requestorEmail },
                ccEmails,
                $"DL Membership Request Completed - {request.DLName}",
                body);
        }

        // Matches PowershellCommandService semantics: "Add" adds, anything else removes.
        private static bool IsAdd(string? action) => string.Equals(action, "Add", StringComparison.Ordinal);

        private static string Enc(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

        private static string FirstNonEmpty(params string?[] values) =>
            values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? string.Empty;
    }
}
