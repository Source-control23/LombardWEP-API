using LombardAPI_NEW.DataBase;
using LombardAPI_NEW.Interfaces;
using LombardAPI_NEW.Models;
using LombardAPI_NEW.Shared;
using Microsoft.EntityFrameworkCore;

namespace LombardAPI_NEW.Services
{
    public class ApprovalService : IApproval
    {
        private readonly DataBaseContext _context;
        private readonly PowershellCommandService _powershellcommand;

        public ApprovalService(DataBaseContext context, PowershellCommandService powershellCommand)
        {
            _context = context;
            _powershellcommand = powershellCommand;
        }

        public async Task<DL> GetRequestasync(string id)
        {
            try
            {
                // Assuming 'Id' is a string or convertable
                var request = await _context.DLDetails
                                            .Where(d => d.ApprovalID == id && d.ApprovalStatus == "Pending")
                                            .FirstOrDefaultAsync();

                return request;
            }
            catch (Exception ex)
            {
                // Log exception
                Console.WriteLine(ex.Message);
                return null;
            }
        }

        public async Task<bool> Approveasync(string id)
        {
            try
            {
                DL req = await GetRequestasync(id);
                if (req == null)
                {
                    return false;
                }

                if (req.Event == "Membership | OnPrem" || req.Event == "Membership | Cloud")
                {
                    _powershellcommand.GroupMembershipCommand(req);

                }
                else if (req.Event == "Rights | Onprem" || req.Event == "Rights | Cloud")
                {
                    _powershellcommand.RightsManagementCommand(req);
                }
                else if (req.Event == "Unlock")
                {
                    _powershellcommand.AccountUnlockCommand(req);
                }

                var requestToUpdate = await _context.DLDetails
                                           .Where(d => d.ApprovalID == id)
                                           .FirstOrDefaultAsync();
                requestToUpdate.ApprovalStatus = "Approved";
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        public async Task<bool> Rejectasync(string id)
        {
            try
            {
                var req = await GetRequestasync(id);
                if (req == null) return false;

                var requestToUpdate = await _context.DLDetails
                                           .Where(d => d.ApprovalID == id)
                                           .FirstOrDefaultAsync();
                requestToUpdate.ApprovalStatus = "Rejected";
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }
    }
}
