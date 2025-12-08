using LombardAPI_NEW.Models.DTOs;
using LombardAPI_NEW.Models.Enums;

namespace LombardAPI_NEW.Interfaces
{
    public interface IGAL
    {

        Task<GALSearchResponseDTO> SearchAsync(GALSearchEnum parameter, string value);

    }
}
