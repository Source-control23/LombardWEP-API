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
        private readonly ILogger<ApprovalService> _logger;

        public ApprovalService(DataBaseContext context, PowershellCommandService powershellCommand, ILogger<ApprovalService> logger)
        {
            _context = context;
            _powershellcommand = powershellCommand;
            _logger = logger;
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

        //public async Task<bool> Approveasync(string id)
        //{
        //    try
        //    {
        //        DL req = await GetRequestasync(id);
        //        if (req == null)
        //        {
        //            return false;
        //        }

        //        if (req.Event == "Membership | OnPrem" || req.Event == "Membership | Cloud")
        //        {
        //            _powershellcommand.GroupMembershipCommand(req);

        //        }
        //        else if (req.Event == "Rights | Onprem" || req.Event == "Rights | Cloud")
        //        {
        //            _powershellcommand.RightsManagementCommand(req);
        //        }
        //        else if (req.Event == "Unlock")
        //        {
        //            _powershellcommand.AccountUnlockCommand(req);
        //        }

        //        var requestToUpdate = await _context.DLDetails
        //                                   .Where(d => d.ApprovalID == id)
        //                                   .FirstOrDefaultAsync();
        //        requestToUpdate.ApprovalStatus = "Approved";
        //        await _context.SaveChangesAsync();

        //        return true;
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine(ex.Message);
        //        return false;
        //    }
        //}

        public async Task<bool> Approveasync(string id)
        {
            _logger.LogInformation("ApproveAsync started. ApprovalID: {ApprovalID}", id);

            try
            {
                _logger.LogInformation("Fetching request details from GetRequestAsync.");

                DL req = await GetRequestasync(id);

                if (req == null)
                {
                    _logger.LogWarning("Request not found. ApprovalID: {ApprovalID}", id);
                    return false;
                }

                _logger.LogInformation("Request fetched successfully. Event: {Event}", req.Event);

                if (req.Event == "Membership | OnPrem" || req.Event == "Membership | Cloud")
                {
                    _logger.LogInformation("Executing GroupMembershipCommand.");
                    _powershellcommand.GroupMembershipCommand(req);
                    _logger.LogInformation("GroupMembershipCommand executed successfully.");
                }
                else if (req.Event == "Rights | OnPrem" || req.Event == "Rights | Cloud")
                {
                    _logger.LogInformation("Executing RightsManagementCommand.");
                    _powershellcommand.RightsManagementCommand(req);
                    _logger.LogInformation("RightsManagementCommand executed successfully.");
                }
                else if (req.Event == "Unlock")
                {
                    _logger.LogInformation("Executing AccountUnlockCommand.");
                    _powershellcommand.AccountUnlockCommand(req);
                    _logger.LogInformation("AccountUnlockCommand executed successfully.");
                }
                else
                {
                    _logger.LogWarning("Unknown event type received: {Event}", req.Event);
                }

                _logger.LogInformation("Fetching DLDetails record to update approval status.");

                var requestToUpdate = await _context.DLDetails
                    .Where(d => d.ApprovalID == id)
                    .FirstOrDefaultAsync();

                if (requestToUpdate == null)
                {
                    _logger.LogWarning("DLDetails record not found for ApprovalID: {ApprovalID}", id);
                    return false;
                }

                _logger.LogInformation("Updating ApprovalStatus to Approved.");

                requestToUpdate.ApprovalStatus = "Approved";
                await _context.SaveChangesAsync();

                _logger.LogInformation("Approval completed successfully. ApprovalID: {ApprovalID}", id);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error occurred in ApproveAsync. ApprovalID: {ApprovalID}", id);

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