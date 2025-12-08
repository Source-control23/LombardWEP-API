using LombardAPI_NEW.Services;
using LombardAPI_NEW.Models.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using LombardAPI_NEW.Interfaces;


namespace LombardAPI_NEW.Controllers
{



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
        public async Task<IActionResult> Search([FromQuery] GALSearchEnum parameter, [FromQuery] string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return BadRequest("Search value cannot be empty.");

            var result = await _gALService.SearchAsync(parameter, value);

            if (result == null)
                return NotFound("No matching records found.");

            return Ok(result);
        }

    }
}
