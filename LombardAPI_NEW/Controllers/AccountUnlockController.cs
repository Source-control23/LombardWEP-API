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
                // Do NOT echo the raw employeeId back into the response body.
                return NotFound(ApiResponse.Fail(
                    "User not found or the lookup could not be completed."
                ));
            }

            // Excessive Data Exposure fix: return an explicit, whitelisted
            // projection instead of handing the entire internal object to the
            // API surface. Add fields here deliberately, never implicitly.
            var response = new
            {
                user.EmployeeId,
                user.IsLocked,
                user.AccountStatus,
                user.AccountExpiry,
                user.PasswordExpiryStatus
            };

            return Ok(ApiResponse.Ok(response));
        }

        [HttpPost("unlock")]
        public async Task<IActionResult> Unlock([FromBody] AccountUnlockRequestDTO request)
        {
            var result = await _service.UnlockAccountAsync(request);
            return Ok(result);
        }
    }

}