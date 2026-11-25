using LombardAPI_NEW.Interfaces;
using LombardAPI_NEW.Models;
using LombardAPI_NEW.Models.DTOs;
using LombardAPI_NEW.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace LombardAPI_NEW.Controllers
{
    /// <summary>
    /// Account Unlock. Token required (401) and the "Feature.Access" app role required (403),
    /// enforced by the <see cref="AuthPolicies.FeatureAccess"/> policy on every action.
    /// </summary>
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

        /// <summary>Returns the AD lock / expiry status of an employee account.</summary>
        /// <param name="employeeId">Employee / NT ID.</param>
        /// <response code="200">Account status.</response>
        /// <response code="401">Not signed in.</response>
        /// <response code="403">Caller does not hold the Feature.Access role.</response>
        /// <response code="404">User not found.</response>
        [HttpGet("{employeeId}")]
        [Authorize(Policy = AuthPolicies.FeatureAccess)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetStatus(string employeeId)
        {
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

        /// <summary>Unlocks an employee's AD account.</summary>
        /// <param name="request">Employee to unlock.</param>
        /// <response code="200">Unlock result.</response>
        /// <response code="400">Invalid request.</response>
        /// <response code="401">Not signed in.</response>
        /// <response code="403">Caller does not hold the Feature.Access role.</response>
        [HttpPost("unlock")]
        [Authorize(Policy = AuthPolicies.FeatureAccess)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Unlock([FromBody] AccountUnlockRequestDTO request)
        {
            var result = await _service.UnlockAccountAsync(request);
            return Ok(result);
        }
    }
}
