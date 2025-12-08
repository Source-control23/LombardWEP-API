using LombardAPI_NEW.Interfaces;
using LombardAPI_NEW.Models;
using LombardAPI_NEW.Models.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace LombardAPI_NEW.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GroupMembershipController : ControllerBase
    {
        private readonly IGroupMembership _service;
        private readonly ILogger<GroupMembershipController> _logger;

        public GroupMembershipController(IGroupMembership service, ILogger<GroupMembershipController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet("{dlName}")]
        public async Task<IActionResult> GetMembers(string dlName)
        {
            try
            {
                var result = await _service.GetByDLNameAsync(dlName);
                return Ok(ApiResponse.Ok(result));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch DL info");
                return BadRequest(ApiResponse.Fail("Unable to fetch DL details."));
            }
        }

        [HttpPost("AddOrRemoveDLMembership")]
        public async Task<IActionResult> AddOrRemove(AddOrRemoveDLMembershipDTO dto)
        {
            try
            {
                bool result = await _service.AddOrRemoveDLMembershipAsync(dto);
                if (!result)
                    return BadRequest(ApiResponse.Fail("Request failed. Email not sent or DB error."));

                return Ok(ApiResponse.Ok("Operation successful"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Membership update failed.");
                return BadRequest(ApiResponse.Fail("Unexpected error occurred."));
            }
        }
    }

}
