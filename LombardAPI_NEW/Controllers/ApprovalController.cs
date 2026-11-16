using LombardAPI_NEW.Interfaces;
using LombardAPI_NEW.Models;
using LombardAPI_NEW.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LombardAPI_NEW.Controllers
{
    /// <summary>
    /// Approval page endpoints. A valid Microsoft Entra ID token is REQUIRED, and the signed-in
    /// user must be one of the request's approvers (the DL owner the email was sent to).
    /// </summary>
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ApprovalController : ControllerBase
    {
        private readonly IApproval _approvalService;
        private readonly ILogger<ApprovalController> _logger;

        public ApprovalController(IApproval approval, ILogger<ApprovalController> logger)
        {
            _approvalService = approval;
            _logger = logger;
        }

        /// <summary>
        /// 200 { status:200, message, data } — data.approvalStatus tells whether actions are allowed (Pending).
        /// 401 not signed in · 403 { status:403, message } not the approver · 404 { status:404, message } invalid link.
        /// </summary>
        [HttpGet("request/{id}")]
        public async Task<IActionResult> GetRequest(string id)
        {
            var outcome = await _approvalService.GetRequestForApproverAsync(id, CallerIdentity.FromPrincipal(User));

            int code = ToHttpStatus(outcome.Status);
            if (outcome.Status == ApprovalOutcomeStatus.Success)
                return Ok(new { status = 200, message = outcome.Message, data = outcome.Request });

            return StatusCode(code, new { status = code, message = outcome.Message });
        }

        /// <summary>200 "Approved Successfully" (text) · 401/403/404/409/500 with a text message.</summary>
        [HttpPost("approve/{id}")]
        public async Task<IActionResult> Approve(string id)
        {
            var outcome = await _approvalService.ApproveAsync(id, CallerIdentity.FromPrincipal(User));
            return TextResult(outcome);
        }

        /// <summary>200 "Rejected Successfully" (text) · 401/403/404/409 with a text message.</summary>
        [HttpPost("reject/{id}")]
        public async Task<IActionResult> Reject(string id)
        {
            var outcome = await _approvalService.RejectAsync(id, CallerIdentity.FromPrincipal(User));
            return TextResult(outcome);
        }

        private IActionResult TextResult(ApprovalOutcome outcome) => new ContentResult
        {
            StatusCode = ToHttpStatus(outcome.Status),
            Content = outcome.Message,
            ContentType = "text/plain; charset=utf-8"
        };

        private static int ToHttpStatus(ApprovalOutcomeStatus status) => status switch
        {
            ApprovalOutcomeStatus.Success => StatusCodes.Status200OK,
            ApprovalOutcomeStatus.Unauthenticated => StatusCodes.Status401Unauthorized,
            ApprovalOutcomeStatus.NotFound => StatusCodes.Status404NotFound,
            ApprovalOutcomeStatus.Forbidden => StatusCodes.Status403Forbidden,
            ApprovalOutcomeStatus.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };
    }
}
