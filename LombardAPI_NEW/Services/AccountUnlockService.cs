using LombardAPI_NEW.DataBase;
using LombardAPI_NEW.Interfaces;
using LombardAPI_NEW.Models;
using LombardAPI_NEW.Models.DTOs;
using LombardAPI_NEW.Shared;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LombardAPI_NEW.Services
{
    public class AccountUnlockService : IAccountUnlock
    {
        private readonly DataBaseContext _context;
        private readonly IEmailService _emailService;

        public AccountUnlockService(DataBaseContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }


        public async Task<AccountUnlockResponseDTO> GetUserDetailsAsync(string employeeId)
        {
            var psCmd = $@"Get-ADUser -Identity {employeeId} -Properties Enabled,LockedOut,AccountExpirationDate,PasswordExpired,info | Select SamAccountName,Enabled,LockedOut,PasswordExpired,AccountExpirationDate,info";

            var json = Powershell.RunPowerShell(psCmd);

            if (string.IsNullOrWhiteSpace(json))
                return null;

            var user = JsonSerializer.Deserialize<JsonElement>(json);

            return new AccountUnlockResponseDTO
            {
                EmployeeId = user.GetProperty("SamAccountName").ToString(),
                IsLocked = bool.Parse(user.GetProperty("LockedOut").ToString()),
                Message = user.GetProperty("info").ToString()
            };
        }

        public async Task<string> SaveToDB(AccountUnlockRequestDTO dto)
        {
            try
            {
                var dl = new DL
                {
                    ApprovalID = Guid.NewGuid().ToString(),
                    Event = "Unlock",
                    DLName = dto.DLName,
                    DLOWner = dto.DLOwner,
                    Action = "",
                    EmpIds = "",
                    Rights = "",
                    IsLocked = dto.Unlock,
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

        public async Task<ApiResponse> UnlockAccountAsync(AccountUnlockRequestDTO request)
        {
            // 1️⃣ Unlock AD
            var unlockCmd = $"Unlock-ADAccount -Identity {request.EmployeeId}";
            Powershell.RunPowerShell(unlockCmd);

            // 2️⃣ Save audit
            var approvalId = await SaveToDB(request);
            if (string.IsNullOrEmpty(approvalId))
                return ApiResponse.Fail("Failed to save approval request.");

            // 3️⃣ Send mail
            var link = $"http://localhost:4200/approval?id={approvalId}";
            var mailSent = await _emailService.SendEmailAsync(
                "",
                "Unlock Lock Account Request",
                BuildMailBody(link)
            );

            if (!mailSent)
                return ApiResponse.Fail("Unlock done but mail failed.");

            return ApiResponse.Ok(new
            {
                EmployeeId = request.EmployeeId,
                Status = "Account Unlocked",
                ApprovalId = approvalId
            });
        }

        private string BuildMailBody(string link)
        {
            return $@"
  <div style='font-family:Segoe UI,Arial,sans-serif; font-size:14px; color:#333; line-height:1.6;'>
      
      <p>Dear Team,</p>

      <p>
          A new <strong>Unlock Lock Account Request</strong> requires your review and approval.
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
        }





    }
}