using LombardAPI_NEW.Interfaces;
using LombardAPI_NEW.Models;
using LombardAPI_NEW.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;

namespace LombardAPI_NEW.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/account-unlock")]
    public class AccountUnlockController : ControllerBase
    {
        private readonly IAccountUnlock _service;

        public AccountUnlockController(IAccountUnlock service)
        {
            _service = service;
        }

        // Checks raw token claims directly — immune to RoleClaimType mapping issues.
        private bool HasFeatureAccess() =>
            User.Claims.Any(c =>
                (c.Type == "roles" || c.Type == "role" ||
                 c.Type.EndsWith("/role") || c.Type.EndsWith("/roles"))
                && c.Value == "Feature.Access");

        [HttpGet("{employeeId}")]
        public async Task<IActionResult> GetStatus(string employeeId)
        {
            if (!HasFeatureAccess())
                return Forbid();

            var user = await _service.GetUserDetailsAsync(employeeId);

            if (user == null)
            {
                return NotFound(ApiResponse.Fail(
                    "User not found or the lookup could not be completed."
                ));
            }

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
            if (!HasFeatureAccess())
                return Forbid();

            var result = await _service.UnlockAccountAsync(request);
            return Ok(result);
        }
    }
}