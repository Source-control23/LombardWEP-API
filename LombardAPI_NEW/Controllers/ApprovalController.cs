using LombardAPI_NEW.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace LombardAPI_NEW.Controllers
{
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

        private bool HasFeatureAccess() =>
            User.Claims.Any(c =>
                (c.Type == "roles" || c.Type == "role" ||
                 c.Type.EndsWith("/role") || c.Type.EndsWith("/roles"))
                && c.Value == "Feature.Access");

        [HttpGet("request/{id}")]
        public async Task<IActionResult> GetRequest(string id)
        {
            if (!HasFeatureAccess())
                return Forbid();

            var req = await _approvalService.GetRequestasync(id);

            if (req == null)
                return Ok(new { status = 404, message = "No request found" });

            return Ok(new { status = 200, message = "Success", data = req });
        }

        [HttpPost("approve/{id}")]
        public async Task<IActionResult> Approve(string id)
        {
            if (!HasFeatureAccess())
                return Forbid();

            var req = await _approvalService.Approveasync(id);
            if (req)
                return Content("Approved Successfully");
            else
                return Content("500 Server Error");
        }

        [HttpPost("reject/{id}")]
        public async Task<IActionResult> Reject(string id)
        {
            if (!HasFeatureAccess())
                return Forbid();

            var req = await _approvalService.Rejectasync(id);
            if (req)
                return Content("Rejected Successfully");
            else
                return Content("500 Server Error");
        }
    }
}