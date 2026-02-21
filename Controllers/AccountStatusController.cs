using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserService.Common;
using UserService.Data;
using UserService.DTOs;

namespace UserService.Controllers;

/// <summary>
/// Account pause/resume endpoints (T090).
/// </summary>
[Route("api/account")]
[ApiController]
[Authorize]
public class AccountStatusController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AccountStatusController> _logger;

    public AccountStatusController(
        ApplicationDbContext context,
        ILogger<AccountStatusController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>Pause (snooze) the authenticated user's account.</summary>
    [HttpPost("pause")]
    public async Task<IActionResult> PauseAccount([FromBody] AccountPauseRequest request)
    {
        var profile = await GetAuthenticatedProfile();
        if (profile == null)
            return NotFound(ApiResponse<string>.FailureResult("Profile not found"));

        if (profile.AccountStatus == "Paused")
            return BadRequest(ApiResponse<string>.FailureResult("Account is already paused"));

        profile.AccountStatus = "Paused";
        profile.PausedAt = DateTime.UtcNow;
        profile.PauseDuration = request.Duration.ToString();
        profile.PauseReason = request.Reason;
        profile.IsActive = false;
        profile.PauseUntil = request.Duration switch
        {
            DTOs.PauseDuration.Hours24 => DateTime.UtcNow.AddHours(24),
            DTOs.PauseDuration.Hours72 => DateTime.UtcNow.AddHours(72),
            DTOs.PauseDuration.OneWeek => DateTime.UtcNow.AddDays(7),
            DTOs.PauseDuration.Indefinite => null,
            _ => null
        };

        profile.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Account {UserId} paused for {Duration}", profile.UserId, request.Duration);

        return Ok(ApiResponse<AccountStatusResponse>.SuccessResult(ToResponse(profile)));
    }

    /// <summary>Resume the authenticated user's account.</summary>
    [HttpPost("resume")]
    public async Task<IActionResult> ResumeAccount()
    {
        var profile = await GetAuthenticatedProfile();
        if (profile == null)
            return NotFound(ApiResponse<string>.FailureResult("Profile not found"));

        if (profile.AccountStatus != "Paused")
            return BadRequest(ApiResponse<string>.FailureResult("Account is not paused"));

        profile.AccountStatus = "Active";
        profile.PausedAt = null;
        profile.PauseUntil = null;
        profile.PauseDuration = null;
        profile.PauseReason = null;
        profile.IsActive = true;
        profile.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Account {UserId} resumed", profile.UserId);

        return Ok(ApiResponse<AccountStatusResponse>.SuccessResult(ToResponse(profile)));
    }

    /// <summary>Get current account status for the authenticated user.</summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetAccountStatus()
    {
        var profile = await GetAuthenticatedProfile();
        if (profile == null)
            return NotFound(ApiResponse<string>.FailureResult("Profile not found"));

        // Auto-resume if pause has expired
        if (profile.AccountStatus == "Paused" && profile.PauseUntil.HasValue && profile.PauseUntil < DateTime.UtcNow)
        {
            profile.AccountStatus = "Active";
            profile.PausedAt = null;
            profile.PauseUntil = null;
            profile.PauseDuration = null;
            profile.PauseReason = null;
            profile.IsActive = true;
            profile.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Account {UserId} auto-resumed (pause expired)", profile.UserId);
        }

        return Ok(ApiResponse<AccountStatusResponse>.SuccessResult(ToResponse(profile)));
    }

    private async Task<Models.UserProfile?> GetAuthenticatedProfile()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return null;

        return await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
    }

    private static AccountStatusResponse ToResponse(Models.UserProfile profile) =>
        new(
            UserId: profile.UserId.ToString(),
            Status: Enum.TryParse<AccountStatus>(profile.AccountStatus, out var s) ? s : AccountStatus.Active,
            PausedAt: profile.PausedAt,
            ResumeAt: profile.PauseUntil,
            PauseDuration: Enum.TryParse<DTOs.PauseDuration>(profile.PauseDuration, out var d) ? d : null,
            PauseReason: profile.PauseReason
        );
}
