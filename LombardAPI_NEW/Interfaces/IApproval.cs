using LombardAPI_NEW.Models;

namespace LombardAPI_NEW.Interfaces
{
    public interface IApproval
    {
        Task<DL> GetRequestasync(string id);
        Task<bool> Approveasync(string id);
        Task<bool> Rejectasync(string id);
    }
}
