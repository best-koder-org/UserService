using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserService.Data;

namespace UserService.Controllers;

/// <summary>
/// Feeds the bot-service onboarding assist: returns recently-created HUMAN (non-bot)
/// profiles so the bot swarm can pre-like fresh testers. Bots are excluded by email
/// (@bot.local) and the IsBot flag. Dev/demo oriented — requires a valid JWT.
/// </summary>
[ApiController]
[Route("api/bot")]
[Authorize]
public class BotOnboardingController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<BotOnboardingController> _logger;

    public BotOnboardingController(ApplicationDbContext context, ILogger<BotOnboardingController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>Return human profiles created after `sinceUtc` (ISO-8601). Bounded by `limit`.</summary>
    [HttpGet("onboarding-candidates")]
    public async Task<IActionResult> GetOnboardingCandidates(
        [FromQuery] DateTime? sinceUtc = null,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        var since = sinceUtc ?? DateTime.UtcNow.AddMinutes(-10);
        var query = _context.UserProfiles
            .Where(p => !p.IsBot
                        && p.CreatedAt >= since
                        && !p.Email.ToLower().EndsWith("@bot.local"))
            .OrderByDescending(p => p.CreatedAt)
            .Take(Math.Clamp(limit, 1, 100));

        var profiles = await query
            .Select(p => new
            {
                profileId = p.Id,
                keycloakId = p.UserId.ToString(),
                name = p.Name,
                gender = p.Gender,
                // Age is a computed (non-mapped) property — compute from DateOfBirth so EF can translate it.
                age = DateTime.Today.Year - p.DateOfBirth.Year,
                city = p.City,
                preferences = p.Preferences
            })
            .ToListAsync(ct);

        _logger.LogInformation("Onboarding candidates: {Count} new human profiles since {Since}",
            profiles.Count, since);

        return Ok(profiles);
    }
}
