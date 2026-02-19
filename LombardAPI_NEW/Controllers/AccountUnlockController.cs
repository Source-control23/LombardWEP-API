using LombardAPI_NEW.Interfaces;
using LombardAPI_NEW.Models;
using LombardAPI_NEW.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace LombardAPI_NEW.Controllers
{
    [ApiController]
    [Route("api/account-unlock")]
    public class AccountUnlockController : ControllerBase
    {
        private readonly IAccountUnlock _service;

        public AccountUnlockController(IAccountUnlock service)
        {
            _service = service;
        }

        [HttpGet("{employeeId}")]
        public async Task<IActionResult> GetStatus(string employeeId)
        {
            var user = await _service.GetUserDetailsAsync(employeeId);

            if (user == null)
            {
                return NotFound(ApiResponse.Fail(
                    $"User {employeeId} not found or PowerShell failed."
                ));
            }

            return Ok(ApiResponse.Ok(user));
        }

        [HttpPost("unlock")]
        public async Task<IActionResult> Unlock([FromBody] AccountUnlockRequestDTO request)
        {
            var result = await _service.UnlockAccountAsync(request);
            return Ok(result);
        }
    }

}
