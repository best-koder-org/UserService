using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Common;
using UserService.DTOs;

namespace UserService.Controllers;

/// <summary>
/// Account pause/resume endpoints (T090 scaffolding).
/// Stubs only — returns 501 NotImplemented until full implementation.
/// </summary>
[Route("api/account")]
[ApiController]
[Authorize]
public class AccountStatusController : ControllerBase
{
    private readonly ILogger<AccountStatusController> _logger;

    public AccountStatusController(ILogger<AccountStatusController> logger)
    {
        _logger = logger;
    }

    /// <summary>Pause (snooze) the authenticated user's account.</summary>
    [HttpPost("pause")]
    public IActionResult PauseAccount([FromBody] AccountPauseRequest request)
    {
        // TODO(T090): Implement account pause logic
        // - Set AccountStatus to Paused
        // - Calculate ResumeAt based on PauseDuration
        // - Notify messaging/matchmaking services
        _logger.LogInformation("Account pause requested with duration {Duration}", request.Duration);
        return StatusCode(501, ApiResponse<string>.FailureResult("Account pause not yet implemented"));
    }

    /// <summary>Resume the authenticated user's account.</summary>
    [HttpPost("resume")]
    public IActionResult ResumeAccount()
    {
        // TODO(T090): Implement account resume logic
        // - Set AccountStatus back to Active
        // - Clear PausedAt/ResumeAt
        // - Notify messaging/matchmaking services
        _logger.LogInformation("Account resume requested");
        return StatusCode(501, ApiResponse<string>.FailureResult("Account resume not yet implemented"));
    }

    /// <summary>Get current account status for the authenticated user.</summary>
    [HttpGet("status")]
    public IActionResult GetAccountStatus()
    {
        // TODO(T090): Query actual account status from DB
        _logger.LogInformation("Account status requested");
        return StatusCode(501, ApiResponse<string>.FailureResult("Account status not yet implemented"));
    }
}
