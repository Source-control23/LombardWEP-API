using LombardAPI_NEW.ErrorHandling;
using LombardAPI_NEW.Interfaces;
using LombardAPI_NEW.Models;
using LombardAPI_NEW.Models.DTOs;
using LombardAPI_NEW.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace LombardAPI_NEW.Controllers
{
    /// <summary>
    /// DL Rights (Send Email access) lookup + requests. Same protection as the DAST-cleared
    /// Account Unlock module: token required ([Authorize] → 401) and the "Feature.Access"
    /// app role required (→ 403, Privilege Escalation fix).
    /// </summary>
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

        // Checks raw token claims directly — immune to RoleClaimType mapping issues.
        private bool HasFeatureAccess() =>
            User.Claims.Any(c =>
                (c.Type == "roles" || c.Type == "role" ||
                 c.Type.EndsWith("/role") || c.Type.EndsWith("/roles"))
                && c.Value == "Feature.Access");

        [HttpGet("{dlName}")]
        public async Task<IActionResult> GetByDLName(string dlName, [FromQuery] string? source)
        {
            if (!HasFeatureAccess())
                return Forbid();

            // CWE-117 Log Forging: strip CR/LF/control chars from user input at entry.
            dlName = System.Text.RegularExpressions.Regex.Replace(dlName ?? string.Empty, @"[\r\n\t\p{Cc}]", string.Empty);

            if (string.IsNullOrWhiteSpace(dlName))
                return BadRequest(new { Message = "DL Name is required." });

            try
            {
                // Input validation BEFORE the value reaches a PowerShell template
                // (PowerShell injection fix): rejects quotes, backtick, '$' and control chars.
                dlName = RequestValidation.NormaliseDLName(dlName);

                // Honour the selected source (previously always the on-prem AD lookup, so
                // cloud-only DLs failed). No source = OnPrem, as before.
                string resolvedSource = string.IsNullOrWhiteSpace(source)
                    ? "OnPrem"
                    : RequestValidation.NormaliseSource(source);

                var result = resolvedSource == "Cloud"
                    ? await _groupService.GetByDLNameCloudAsync(dlName)
                    : await _groupService.GetByDLNameAsync(dlName);

                return Ok(result);
            }
            catch (ServiceException ex)
            {
                _logger.LogWarning(ex, "DL lookup failed for {DLName}", LogSanitizer.Clean(dlName));
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch DL details for {DLName}", LogSanitizer.Clean(dlName));
                return StatusCode(500, new { Message = "Internal server error." });
            }
        }

        [HttpPost("AddOrRemoveRights")]
        public async Task<IActionResult> AddOrRemoveRights(AddOrRemoveRightsDTO dto)
        {
            if (!HasFeatureAccess())
                return Forbid();

            if (dto == null || !ModelState.IsValid)
                return BadRequest(new { Message = "Invalid request payload." });

            // CWE-117 Log Forging: strip CR/LF/control chars from user input at entry.
            dto.DLName = System.Text.RegularExpressions.Regex.Replace(dto.DLName ?? string.Empty, @"[\r\n\t\p{Cc}]", string.Empty);

            try
            {
                bool success = await _rightsService.AddOrRemoveRightsAsync(dto);

                if (success)
                    return Ok(new { Message = ApprovalMessages.SentToOwner });

                return BadRequest(new { Message = "Operation failed." });
            }
            catch (ServiceException ex)
            {
                _logger.LogWarning(ex, "Validation/business error during rights request.");
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during rights update for DL {DLName}", LogSanitizer.Clean(dto.DLName));
                return StatusCode(500, new { Message = "Internal server error." });
            }
        }
    }
}
