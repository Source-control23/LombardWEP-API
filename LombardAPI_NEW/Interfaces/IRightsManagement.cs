using LombardAPI_NEW.Models.DTOs;

namespace LombardAPI_NEW.Interfaces
{
    public interface IRightsManagement
    {
        Task<bool> AddOrRemoveRightsAsync(AddOrRemoveRightsDTO dto);
    }
}
