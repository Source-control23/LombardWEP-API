using LombardAPI_NEW.ErrorHandling;
using LombardAPI_NEW.Interfaces;
using LombardAPI_NEW.Models;
using LombardAPI_NEW.Models.DTOs;
using LombardAPI_NEW.Shared;
using Microsoft.AspNetCore.Mvc;

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


    [HttpGet("{dlName}")]
    public async Task<IActionResult> GetMembers(string dlName, [FromQuery] string source)
    {
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

    [HttpPost("AddOrRemoveDLMembership")]
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
            return Ok(ApiResponse.Ok("Operation successful"));
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
    [HttpGet("TestDB")]
    public async Task<IActionResult> TestDB()
    {
        var res = await _service.TestDBConnectionAsync();
        return Ok(res);
    }

}