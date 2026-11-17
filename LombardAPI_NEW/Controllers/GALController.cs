using LombardAPI_NEW.Interfaces;
using LombardAPI_NEW.Models.DTOs;
using LombardAPI_NEW.Models.Enums;
using LombardAPI_NEW.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LombardAPI_NEW.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class GALController : ControllerBase
    {
        private readonly IGAL _gALService;

        public GALController(IGAL gALService)
        {
            _gALService = gALService;
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search(
            [FromQuery] GALSearchEnum parameter,
            [FromQuery] string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return BadRequest("Search value cannot be empty.");

            var result = await _gALService.SearchAsync(parameter, value);

            if (result == null)
                return Ok(new List<GALSearchResponseDTO>());

            var normalized = result is IEnumerable<GALSearchResponseDTO> list
                ? list.ToList()
                : new List<GALSearchResponseDTO> { (GALSearchResponseDTO)result };

            return Ok(normalized);
        }
    }
}