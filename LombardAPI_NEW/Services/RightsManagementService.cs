using LombardAPI_NEW.DataBase;
using LombardAPI_NEW.Interfaces;
using LombardAPI_NEW.Models;
using LombardAPI_NEW.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace LombardAPI_NEW.Services
{
    public class RightsManagementService : IRightsManagement
    {
        private readonly DataBaseContext _context;
        private readonly IEmailService _emailService;

        public RightsManagementService(DataBaseContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }
        public async Task<bool> AddOrRemoveRightsAsync(AddOrRemoveRightsDTO dto)
        {
            try
            {
                string result = await SaveToDB(dto);
                if (result == "")
                {
                    return false;
                }
                string link = $"http://localhost:4200/approval?id={result}";
                string body = $@"
  <div style='font-family:Segoe UI,Arial,sans-serif; font-size:14px; color:#333; line-height:1.6;'>
      
      <p>Dear Team,</p>

      <p>
          A new <strong>DL Rights Approval</strong> requires your review and approval.
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
                bool emailResult = await _emailService.SendEmailAsync("", "DL Rights Approval", body);

                if (!emailResult)
                {
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<string> SaveToDB(AddOrRemoveRightsDTO dto)
        {
            try
            {
                var dl = new DL
                {
                    ApprovalID = Guid.NewGuid().ToString(),
                    Event = "Rights",
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

                await _context.DLDetails.AddAsync(dl);
                await _context.SaveChangesAsync();

                return dl.ApprovalID;
            }
            catch (Exception ex)
            {
                return "";
            }
        }
    }
}