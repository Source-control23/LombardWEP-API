using LombardAPI_NEW.Models.DTOs;

namespace LombardAPI_NEW.Interfaces
{
    public interface IGroupMembership
    {
        Task<AddOrRemoveDLMembershipDTO> GetByDLNameAsync(string dlName);
        Task<AddOrRemoveDLMembershipDTO> GetByDLNameCloudAsync(string dlName);
        Task<bool> AddOrRemoveDLMembershipAsync(AddOrRemoveDLMembershipDTO dto);
    }
}
