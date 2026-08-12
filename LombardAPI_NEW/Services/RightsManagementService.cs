using LombardAPI_NEW.DataBase;
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

        public RightsManagementService(CommonService commonService,
            DataBaseContext context,
            IEmailService emailService,
            ILogger<RightsManagementService> logger, IOptions<PowerShellCommandSettings> options)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
            _cmd = options.Value;
            _commonService = commonService;
        }

        public async Task<bool> AddOrRemoveRightsAsync(AddOrRemoveRightsDTO dto)
        {
            // CWE-117: strip CR/LF from user input on the data-flow path at entry (Checkmarx-recognised inline sanitizer).
            dto.DLName = (dto.DLName ?? string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty);
            try
            {
                string approvalId = await SaveToDB(dto);

                if (string.IsNullOrEmpty(approvalId))
                {
                    _logger.LogWarning("DB save failed for DL {DLName}", LogSanitizer.Clean(dto.DLName));
                    return false;
                }

                string link = $"{_commonService.GetEmailBaseUrl()}approval?id={approvalId}";

                string body = $@"
<div style='font-family:Segoe UI,Arial,sans-serif; font-size:14px; color:#333; line-height:1.6;'>
    <p>Dear Team,</p>

    <p>A new <strong>DL Rights Approval</strong> requires your review.</p>

    <p style='margin:20px 0;'>
        <a href='{link}' 
           style='background-color:#0052cc; color:#fff; padding:10px 18px;
                  text-decoration:none; border-radius:6px; font-weight:600;'>
            Open Approval Page
        </a>
    </p>

    <p>If the button does not work, copy & paste this link:</p>

    <p style='word-break:break-all; color:#0052cc;'>{link}</p>

    <hr style='border:none; border-top:1px solid #e0e0e0; margin:25px 0;' />

    <p style='font-size:12px; color:#777;'>This is an automated email. Please do not reply.</p>
</div>";

                bool emailSent = await _emailService.SendEmailAsync("", "DL Rights Approval", body);

                if (!emailSent)
                {
                    _logger.LogError("Email sending failed for ApprovalID {ApprovalId}", approvalId);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error occurred in AddOrRemoveRightsAsync for DL {DLName}", LogSanitizer.Clean(dto.DLName));
                return false;
            }
        }

        public async Task<string> SaveToDB(AddOrRemoveRightsDTO dto)
        {
            // CWE-117: strip CR/LF from user input on the data-flow path at entry (Checkmarx-recognised inline sanitizer).
            dto.DLName = (dto.DLName ?? string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty);
            try
            {
                DL dlEntry = new DL
                {
                    ApprovalID = Guid.NewGuid().ToString(),
                    Event = dto.Source == "OnPrem" ? "Rights | OnPrem" : "Rights | Cloud",
                    DLName = dto.DLName,
                    DLOWner = dto.DLOwner,
                    Action = dto.Action,
                    EmpIds = "",
                    Rights = dto.Rights,
                    IsLocked = false,
                    CreatedTime = DateTime.UtcNow,
                    ApprovalStatus = "Pending",
                    ApproverEmail = null
                };

                await _context.DLDetails.AddAsync(dlEntry);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Saved DL Rights request. ApprovalID: {Id}", dlEntry.ApprovalID);

                return dlEntry.ApprovalID;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DB insert failed for DL {DLName}", LogSanitizer.Clean(dto.DLName));
                return "";
            }
        }
    }
}