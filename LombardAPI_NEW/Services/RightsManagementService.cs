using LombardAPI_NEW.DataBase;
using LombardAPI_NEW.ErrorHandling;
using LombardAPI_NEW.Interfaces;
using LombardAPI_NEW.Models;
using LombardAPI_NEW.Models.DTOs;
using LombardAPI_NEW.Shared;
using Microsoft.Extensions.Options;

namespace LombardAPI_NEW.Services
{
    public class RightsManagementService : IRightsManagement
    {
        private readonly DataBaseContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<RightsManagementService> _logger;
        private readonly PowerShellCommandSettings _cmd;
        private readonly CommonService _commonService;
        private readonly IGroupMembership _groupService;
        private readonly IMembershipNotificationService _notificationService;
        private readonly EmailRecipientResolver _recipientResolver;
        private readonly CurrentUserService _currentUser;

        public RightsManagementService(CommonService commonService,
            DataBaseContext context,
            IEmailService emailService,
            ILogger<RightsManagementService> logger, IOptions<PowerShellCommandSettings> options,
            IGroupMembership groupService,
            IMembershipNotificationService notificationService,
            EmailRecipientResolver recipientResolver,
            CurrentUserService currentUser)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
            _cmd = options.Value;
            _commonService = commonService;
            _groupService = groupService;
            _notificationService = notificationService;
            _recipientResolver = recipientResolver;
            _currentUser = currentUser;
        }

        /// <summary>
        /// Saves the Rights (Send Email access) request and emails the DL owner for approval.
        /// Validation / business errors are raised as ServiceException with a user-facing message.
        /// </summary>
        public async Task<bool> AddOrRemoveRightsAsync(AddOrRemoveRightsDTO dto)
        {
            if (dto == null)
                throw new ServiceException("Invalid request payload.");

            // CWE-117 Log Forging: whitelist-validate user input at entry (Checkmarx "How to avoid it" #1).
            // Inline on the taint path so the engine recognises it; strips CR/LF/control chars, neutralising every downstream log.
            dto.DLName = System.Text.RegularExpressions.Regex.Replace(dto.DLName ?? string.Empty, @"[\r\n\t\p{Cc}]", string.Empty);

            try
            {
                dto.DLName = RequestValidation.NormaliseDLName(dto.DLName);
                dto.Action = RequestValidation.NormaliseAction(dto.Action);
                dto.Source = RequestValidation.NormaliseSource(dto.Source);
                // Rights = the ID(s) Send Email access is granted to / removed from. They are
                // placed in PowerShell templates and used as CC addresses, so validate strictly.
                dto.Rights = RequestValidation.NormaliseEmployeeIds(dto.Rights);

                var (requestorId, requestorName) = _currentUser.ResolveRequestor(dto.RequestorId, dto.RequestorName);
                dto.RequestorId = requestorId;
                dto.RequestorName = requestorName;

                // The DL owner is the approver: always resolved server-side (OnPrem AD or Exchange
                // Online, matching the selected source) instead of trusting the client value.
                dto.DLOwner = await ResolveDLOwnerAsync(dto);

                var ownerEmails = _recipientResolver.ResolveOwnerEmails(dto.DLOwner);
                if (ownerEmails.Count == 0)
                    throw new ServiceException("Unable to determine the DL owner's email address. Approval email cannot be sent.");

                DL? saved = await SaveToDB(dto, string.Join(",", ownerEmails));
                if (saved == null)
                    throw new ServiceException("Failed to save request in DB.");

                bool emailSent = await _notificationService.SendApprovalRequestAsync(saved);
                if (!emailSent)
                {
                    _logger.LogError("Email sending failed for ApprovalID {ApprovalId}", saved.ApprovalID);
                    throw new ServiceException("Failed to send approval email.");
                }

                return true;
            }
            catch (ServiceException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error occurred in AddOrRemoveRightsAsync for DL {DLName}", System.Text.RegularExpressions.Regex.Replace(dto.DLName ?? string.Empty, @"[\r\n\t\p{Cc}]", string.Empty));
                throw new ServiceException("Rights request failed.", ex);
            }
        }

        private async Task<string> ResolveDLOwnerAsync(AddOrRemoveRightsDTO dto)
        {
            // Same lookup the Rights screen uses for the selected source.
            AddOrRemoveDLMembershipDTO ownerInfo = dto.Source == "Cloud"
                ? await _groupService.GetByDLNameCloudAsync(dto.DLName!)
                : await _groupService.GetByDLNameAsync(dto.DLName!);

            string? resolvedOwner = ownerInfo?.DLOwner?.Trim();

            if (string.IsNullOrWhiteSpace(resolvedOwner) ||
                string.Equals(resolvedOwner, "No Owner Assigned", StringComparison.OrdinalIgnoreCase))
            {
                throw new ServiceException("No owner is assigned to this DL. Approval email cannot be sent.");
            }

            if (!string.IsNullOrWhiteSpace(dto.DLOwner) &&
                !string.Equals(dto.DLOwner.Trim(), resolvedOwner, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Submitted DL owner differs from directory owner. Directory value will be used.");
            }

            return resolvedOwner;
        }

        private async Task<DL?> SaveToDB(AddOrRemoveRightsDTO dto, string approverEmails)
        {
            // CWE-117 Log Forging: whitelist-validate user input at entry (Checkmarx "How to avoid it" #1).
            // Inline on the taint path so the engine recognises it; strips CR/LF/control chars, neutralising every downstream log.
            dto.DLName = System.Text.RegularExpressions.Regex.Replace(dto.DLName ?? string.Empty, @"[\r\n\t\p{Cc}]", string.Empty);

            try
            {
                DL dlEntry = new DL
                {
                    ApprovalID = Guid.NewGuid().ToString(),
                    Event = dto.Source == "OnPrem" ? "Rights | OnPrem" : "Rights | Cloud",
                    DLName = dto.DLName,
                    DLOWner = dto.DLOwner!,
                    Action = dto.Action!,
                    EmpIds = "",
                    Rights = dto.Rights!,
                    IsLocked = false,
                    CreatedTime = DateTime.UtcNow,
                    ApprovalStatus = "Pending",
                    ApproverEmail = approverEmails,
                    RequestorId = dto.RequestorId,
                    RequestorName = dto.RequestorName
                };

                await _context.DLDetails.AddAsync(dlEntry);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Saved DL Rights request. ApprovalID: {Id}", dlEntry.ApprovalID);

                return dlEntry;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DB insert failed for DL {DLName}", System.Text.RegularExpressions.Regex.Replace(dto.DLName ?? string.Empty, @"[\r\n\t\p{Cc}]", string.Empty));
                return null;
            }
        }
    }
}
