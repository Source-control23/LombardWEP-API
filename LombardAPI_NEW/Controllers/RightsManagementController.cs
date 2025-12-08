using LombardAPI_NEW.Interfaces;
using LombardAPI_NEW.Models.DTOs;
using LombardAPI_NEW.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LombardAPI_NEW.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RightsManagementController : ControllerBase
    {

        private readonly IRightsManagement _rightsManagementService;
        private readonly IGroupMembership _groupMembershipService;

        public RightsManagementController(IRightsManagement rightsManagementService, IGroupMembership groupMembershipService)
        {
            _rightsManagementService = rightsManagementService;
            _groupMembershipService = groupMembershipService;
        }

        [HttpGet("{dlName}")]
        public async Task<IActionResult> GetByDLName(string dlName)
        {
            var result = await _groupMembershipService.GetByDLNameAsync(dlName);
            return Ok(result);
        }

        [HttpPost("AddOrRemoveRights")]
        public async Task<IActionResult> AddOrRemoveRights(AddOrRemoveRightsDTO dto)
        {

            var success = await _rightsManagementService.AddOrRemoveRightsAsync(dto);
            if (success)
                return Ok(new { Message = "Request has been submitted." });

            return BadRequest(new { Message = "Operation failed" });

        }

    }
}
