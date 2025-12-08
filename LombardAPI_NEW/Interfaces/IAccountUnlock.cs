using System.Threading.Tasks;
using LombardAPI_NEW.Models;
using LombardAPI_NEW.Models.DTOs;

namespace LombardAPI_NEW.Interfaces
{
    public interface IAccountUnlock
    {
        Task<DL> GetUserDetailsAsync(string employeeId);
        Task<bool> UnlockAccountAsync(AccountUnlockRequestDTO request);
    }
}
