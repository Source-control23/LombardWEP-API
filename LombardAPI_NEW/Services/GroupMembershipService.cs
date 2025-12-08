using LombardAPI_NEW.DataBase;
using LombardAPI_NEW.Interfaces;
using LombardAPI_NEW.Models;
using LombardAPI_NEW.Models.DTOs;
using LombardAPI_NEW.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LombardAPI_NEW.Services
{
    public class GroupMembershipService : IGroupMembership
    {
        private readonly DataBaseContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<GroupMembershipService> _logger;

        public GroupMembershipService(
            DataBaseContext context,
            IEmailService emailService,
            ILogger<GroupMembershipService> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<AddOrRemoveDLMembershipDTO> GetByDLNameAsync(string dlName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dlName))
                {
                    throw new ArgumentException("DL name cannot be null or empty");
                }

                string command = $"Get-ADGroupMember -Identity \"{dlName}\"";

                var result = Powershell.RunPowerShell(command);

                return new AddOrRemoveDLMembershipDTO
                {
                    DLName = dlName,
                    DLOwner = "Test"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching DL members for {DLName}", dlName);
                throw;
            }
        }

        public async Task<bool> AddOrRemoveDLMembershipAsync(AddOrRemoveDLMembershipDTO dto)
        {
            try
            {
                if (dto == null)
                    throw new ArgumentNullException(nameof(dto));

                string savedId = await SaveToDB(dto);

                if (string.IsNullOrEmpty(savedId))
                {
                    _logger.LogWarning("Failed to save DL request for DL: {DLName}", dto.DLName);
                    return false;
                }

                string link = $"http://localhost:4200/approval?id={savedId}";

                string body = $@"
  <div style='font-family:Segoe UI,Arial,sans-serif; font-size:14px; color:#333; line-height:1.6;'>
      
      <p>Dear Team,</p>

      <p>
          A new <strong>DL Membership Request</strong> requires your review and approval.
      </p>

      <p>
          Please click the button below to open the approval page:
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

      <hr style='border:none; border-top:1px solid #e0e0e0; margin:25px 0;' />

      <p style='font-size:12px; color:#777;'>
          This is an automated notification. Please do not reply to this email.
      </p>

  </div>";


                bool emailSent = await _emailService.SendEmailAsync(
                     "",
                    "DL Membership Approval",
                    body
                );

                if (!emailSent)
                {
                    _logger.LogError("Email sending failed for DL request - ID: {ApprovalId}", savedId);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing membership update for DL: {DLName}", dto.DLName);
                return false;
            }
        }

        private async Task<string> SaveToDB(AddOrRemoveDLMembershipDTO dto)
        {
            try
            {
                var dl = new DL
                {
                    ApprovalID = Guid.NewGuid().ToString(),
                    Event = "Membership",
                    DLName = dto.DLName,
                    DLOWner = dto.DLOwner,
                    Action = dto.Action,
                    EmpIds = dto.EmpIds,
                    Rights = "",
                    IsLocked = false,
                    CreatedTime = DateTime.UtcNow,
                    ApprovalStatus = "Pending",
                    ApproverEmail = ""
                };

                await _context.DLDetails.AddAsync(dl);
                await _context.SaveChangesAsync();

                _logger.LogInformation("DL request saved successfully. ApprovalID: {Id}", dl.ApprovalID);
                return dl.ApprovalID;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database save failed for DL: {DLName}", dto.DLName);
                return "";
            }
        }
    }
}
