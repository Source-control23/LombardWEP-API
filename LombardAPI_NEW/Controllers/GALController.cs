using LombardAPI_NEW.Interfaces;
using LombardAPI_NEW.Models.DTOs;
using LombardAPI_NEW.Models.Enums;
using LombardAPI_NEW.Services;
using LombardAPI_NEW.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LombardAPI_NEW.Controllers
{
    /// <summary>
    /// GAL (Global Address List) search. Available to every signed-in user,
    /// enforced by the <see cref="AuthPolicies.SignedInUser"/> policy.
    /// </summary>
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

        /// <summary>Searches the GAL by the selected attribute.</summary>
        /// <param name="parameter">Attribute to search (EmpId, FirstName, LastName, Mail, DisplayName).</param>
        /// <param name="value">Search text.</param>
        /// <response code="200">Matching users (empty list when none).</response>
        /// <response code="400">Empty search value.</response>
        /// <response code="401">Not signed in.</response>
        [HttpGet("search")]
        [Authorize(Policy = AuthPolicies.SignedInUser)]
        [ProducesResponseType(typeof(List<GALSearchResponseDTO>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
