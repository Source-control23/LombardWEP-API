using LombardAPI_NEW.ErrorHandling;
using LombardAPI_NEW.Interfaces;
using LombardAPI_NEW.Models;
using LombardAPI_NEW.Models.DTOs;
using LombardAPI_NEW.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

/// <summary>
/// DL membership lookup + Add/Remove requests. Same protection as the DAST-cleared
/// Account Unlock module: a valid Entra ID token is required ([Authorize] → 401) and the
/// caller must hold the "Feature.Access" app role (→ 403, Privilege Escalation fix), enforced by
/// the <see cref="AuthPolicies.FeatureAccess"/> policy declared on every action.
/// </summary>
[Authorize]
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

    /// <summary>Looks up a DL (owner and members) in on-prem AD or Exchange Online.</summary>
    /// <param name="dlName">Distribution list name.</param>
    /// <param name="source">OnPrem or Cloud.</param>
    /// <response code="200">DL details.</response>
    /// <response code="400">Invalid DL name / source, or DL not found.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Caller does not hold the Feature.Access role.</response>
    /// <response code="500">Unexpected error.</response>
    [HttpGet("{dlName}")]
    [Authorize(Policy = AuthPolicies.FeatureAccess)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetMembers(string dlName, [FromQuery] string source)
    {
        // CWE-117 Log Forging: strip CR/LF/control chars from user input at entry.
        dlName = System.Text.RegularExpressions.Regex.Replace(dlName ?? string.Empty, @"[\r\n\t\p{Cc}]", string.Empty);

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["DLName"] = LogSanitizer.Clean(dlName)
        });

        try
        {
            if (string.IsNullOrWhiteSpace(dlName))
            {
                _logger.LogWarning("Invalid DL name received.");
                return BadRequest(ApiResponse.Fail("DL name cannot be empty."));
            }

            // Input validation BEFORE the value reaches a PowerShell template
            // (PowerShell injection fix): rejects quotes, backtick, '$' and control chars.
            dlName = RequestValidation.NormaliseDLName(dlName);

            if (string.IsNullOrWhiteSpace(source))
                return BadRequest(ApiResponse.Fail("Invalid source value. Allowed: OnPrem, Cloud."));

            _logger.LogInformation("Fetching DL members...");
            AddOrRemoveDLMembershipDTO result;

            if (source.Equals("OnPrem", StringComparison.OrdinalIgnoreCase))
            {
                result = await _service.GetByDLNameAsync(dlName);
            }
            else if (source.Equals("Cloud", StringComparison.OrdinalIgnoreCase))
            {
                result = await _service.GetByDLNameCloudAsync(dlName);
            }
            else
            {
                return BadRequest(ApiResponse.Fail("Invalid source value. Allowed: OnPrem, Cloud."));
            }
            _logger.LogInformation("DL info fetched successfully.");

            return Ok(ApiResponse.Ok(result));
        }
        catch (ServiceException ex)
        {
            _logger.LogWarning(ex, "Business validation failed.");
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while fetching DL info.");
            return StatusCode(500, ApiResponse.Fail("Internal server error."));
        }
    }

    /// <summary>Creates a DL membership Add/Remove request and emails the DL owner for approval.</summary>
    /// <param name="dto">DL name, action, employee / consultant IDs and source.</param>
    /// <response code="200">Request sent to the DL owner.</response>
    /// <response code="400">Validation failed or the request could not be created.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Caller does not hold the Feature.Access role.</response>
    /// <response code="500">Unexpected error.</response>
    [HttpPost("AddOrRemoveDLMembership")]
    [Authorize(Policy = AuthPolicies.FeatureAccess)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AddOrRemove(AddOrRemoveDLMembershipDTO dto)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["DLName"] = LogSanitizer.Clean(dto?.DLName),
            ["Action"] = LogSanitizer.Clean(dto?.Action)
        });

        try
        {
            _logger.LogInformation("Processing DL membership request...");

            bool result = await _service.AddOrRemoveDLMembershipAsync(dto);

            if (!result)
            {
                _logger.LogWarning("DL membership request failed.");
                return BadRequest(ApiResponse.Fail("Request failed. Email not sent or DB error."));
            }

            _logger.LogInformation("DL membership operation successful.");
            return Ok(new ApiResponse
            {
                Success = true,
                Message = ApprovalMessages.SentToOwner,
                Data = ApprovalMessages.SentToOwner
            });
        }
        catch (ServiceException ex)
        {
            _logger.LogWarning(ex, "Validation/business error occurred.");
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected exception during membership update.");
            return StatusCode(500, ApiResponse.Fail("Unexpected internal error."));
        }
    }

    /// <summary>Database connectivity check (Feature.Access holders only).</summary>
    /// <response code="200">Connectivity result text.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Caller does not hold the Feature.Access role.</response>
    [HttpGet("TestDB")]
    [Authorize(Policy = AuthPolicies.FeatureAccess)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> TestDB()
    {
        _logger.LogInformation("Database connectivity check requested.");
        var res = await _service.TestDBConnectionAsync();
        return Ok(res);
    }
}
