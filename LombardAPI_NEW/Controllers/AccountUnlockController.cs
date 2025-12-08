using LombardAPI_NEW.Interfaces;
using LombardAPI_NEW.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace LombardAPI_NEW.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountUnlockController : ControllerBase
    {
        private readonly IAccountUnlock _accountUnlockService;

        public AccountUnlockController(IAccountUnlock accountUnlockService)
        {
            _accountUnlockService = accountUnlockService;
        }

        [HttpGet("{employeeId}")]
        public async Task<IActionResult> GetUserDetails(string employeeId)
        {
            var user = await _accountUnlockService.GetUserDetailsAsync(employeeId);

            if (user == null)
                return NotFound(new { Message = "User not found in Active Directory." });

            if (!user.IsLocked)
                return BadRequest(new { Message = "User account is already unlocked." });

            return Ok(new
            {
                Message = "User found and account is locked.",
                UserDetails = user
            });
        }

        [HttpPost("unlock")]
        public async Task<IActionResult> UnlockAccount([FromBody] AccountUnlockRequestDTO request)
        {
            var result = await _accountUnlockService.UnlockAccountAsync(request);
            return Ok(result);
        }
    }
}
