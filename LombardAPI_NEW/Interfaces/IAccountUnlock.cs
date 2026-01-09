using System.Threading.Tasks;
using LombardAPI_NEW.Models;
using LombardAPI_NEW.Models.DTOs;

namespace LombardAPI_NEW.Interfaces
{
    public interface IAccountUnlock
    {
        Task<AccountUnlockResponseDTO> GetUserDetailsAsync(string employeeId);
        Task<ApiResponse> UnlockAccountAsync(AccountUnlockRequestDTO request);
    }
}
