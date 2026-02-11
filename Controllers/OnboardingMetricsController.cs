using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserService.Data;
using UserService.DTOs;
using UserService.Models;

namespace UserService.Controllers;

/// <summary>
/// Admin endpoint for onboarding funnel metrics.
/// Provides aggregate statistics on user registration and onboarding completion.
/// </summary>
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class OnboardingMetricsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<OnboardingMetricsController> _logger;

    public OnboardingMetricsController(ApplicationDbContext context, ILogger<OnboardingMetricsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get onboarding funnel metrics — aggregate counts per step.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<OnboardingMetricsDto>> GetMetrics()
    {
        var totalRegistered = await _context.UserProfiles.CountAsync();

        // Basic info complete: has name, email, gender, and date of birth set
        var completedBasicInfo = await _context.UserProfiles
            .CountAsync(u => !string.IsNullOrEmpty(u.Name) &&
                             !string.IsNullOrEmpty(u.Gender) &&
                             u.DateOfBirth != default);

        // Photos: at least one photo uploaded
        var completedPhotos = await _context.UserProfiles
            .CountAsync(u => u.PhotoCount > 0);

        // Preferences: user has set match preferences
        var completedPreferences = await _context.MatchPreferences.CountAsync();

        // Fully onboarded: OnboardingStatus == Ready
        var fullyOnboarded = await _context.UserProfiles
            .CountAsync(u => u.OnboardingStatus == OnboardingStatus.Ready);

        var conversionRate = totalRegistered > 0
            ? Math.Round((double)fullyOnboarded / totalRegistered * 100, 1)
            : 0;

        return Ok(new OnboardingMetricsDto
        {
            TotalRegistered = totalRegistered,
            CompletedBasicInfo = completedBasicInfo,
            CompletedPhotos = completedPhotos,
            CompletedPreferences = completedPreferences,
            FullyOnboarded = fullyOnboarded,
            ConversionRate = conversionRate
        });
    }

    /// <summary>
    /// Get per-step breakdown of onboarding completion.
    /// </summary>
    [HttpGet("steps")]
    public async Task<ActionResult<List<OnboardingStepMetric>>> GetStepBreakdown()
    {
        var total = await _context.UserProfiles.CountAsync();
        if (total == 0)
        {
            return Ok(new List<OnboardingStepMetric>());
        }

        var basicInfoCount = await _context.UserProfiles
            .CountAsync(u => !string.IsNullOrEmpty(u.Name) &&
                             !string.IsNullOrEmpty(u.Gender) &&
                             u.DateOfBirth != default);

        var photosCount = await _context.UserProfiles
            .CountAsync(u => u.PhotoCount > 0);

        var prefsCount = await _context.MatchPreferences.CountAsync();

        var readyCount = await _context.UserProfiles
            .CountAsync(u => u.OnboardingStatus == OnboardingStatus.Ready);

        var steps = new List<OnboardingStepMetric>
        {
            new() { StepName = "BasicInfo", CompletedCount = basicInfoCount, PendingCount = total - basicInfoCount, CompletionPercentage = Math.Round((double)basicInfoCount / total * 100, 1) },
            new() { StepName = "Photos", CompletedCount = photosCount, PendingCount = total - photosCount, CompletionPercentage = Math.Round((double)photosCount / total * 100, 1) },
            new() { StepName = "Preferences", CompletedCount = prefsCount, PendingCount = total - prefsCount, CompletionPercentage = Math.Round((double)prefsCount / total * 100, 1) },
            new() { StepName = "FullyOnboarded", CompletedCount = readyCount, PendingCount = total - readyCount, CompletionPercentage = Math.Round((double)readyCount / total * 100, 1) }
        };

        return Ok(steps);
    }
}
