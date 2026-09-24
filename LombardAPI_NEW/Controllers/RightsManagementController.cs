using LombardAPI_NEW.Interfaces;
using LombardAPI_NEW.Models.DTOs;
using LombardAPI_NEW.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace LombardAPI_NEW.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class RightsManagementController : ControllerBase
    {
        private readonly IRightsManagement _rightsService;
        private readonly IGroupMembership _groupService;
        private readonly ILogger<RightsManagementController> _logger;

        public RightsManagementController(
            IRightsManagement rightsService,
            IGroupMembership groupService,
            ILogger<RightsManagementController> logger)
        {
            _rightsService = rightsService;
            _groupService = groupService;
            _logger = logger;
        }

        private bool HasFeatureAccess() =>
            User.Claims.Any(c =>
                (c.Type == "roles" || c.Type == "role" ||
                 c.Type.EndsWith("/role") || c.Type.EndsWith("/roles"))
                && c.Value == "Feature.Access");

        [HttpGet("{dlName}")]
        public async Task<IActionResult> GetByDLName(string dlName)
        {
            if (!HasFeatureAccess())
                return Forbid();

            dlName = System.Text.RegularExpressions.Regex.Replace(dlName ?? string.Empty, @"[\r\n\t\p{Cc}]", string.Empty);

            if (string.IsNullOrWhiteSpace(dlName))
                return BadRequest(new { Message = "DL Name is required." });

            try
            {
                var result = await _groupService.GetByDLNameAsync(dlName);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch DL details for {DLName}", dlName);
                return StatusCode(500, new { Message = "Internal server error." });
            }
        }

        [HttpPost("AddOrRemoveRights")]
        public async Task<IActionResult> AddOrRemoveRights(AddOrRemoveRightsDTO dto)
        {
            if (!HasFeatureAccess())
                return Forbid();

            dto.DLName = System.Text.RegularExpressions.Regex.Replace(dto.DLName ?? string.Empty, @"[\r\n\t\p{Cc}]", string.Empty);

            if (!ModelState.IsValid)
                return BadRequest(new { Message = "Invalid request payload." });

            try
            {
                bool success = await _rightsService.AddOrRemoveRightsAsync(dto);

                if (success)
                    return Ok(new { Message = "Request has been submitted." });

                return BadRequest(new { Message = "Operation failed." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during rights update for DL {DLName}", dto.DLName);
                return StatusCode(500, new { Message = "Internal server error." });
            }
        }
    }
}