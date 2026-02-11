using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using UserService.Data;
using UserService.DTOs;
using UserService.Models;

namespace UserService.Controllers;

[ApiController]
[Route("api/userprofiles/{userId}/[controller]")]
[Authorize]
public class PreferencesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PreferencesController> _logger;

    public PreferencesController(ApplicationDbContext context, ILogger<PreferencesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    private string GetCurrentUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? throw new UnauthorizedAccessException("User ID not found in token");

    /// <summary>
    /// Get match preferences for a user
    /// GET /api/userprofiles/{userId}/preferences
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(GetPreferencesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GetPreferencesResponse>> GetPreferences(string userId)
    {
        var currentUserId = GetCurrentUserId();

        // Users can only read their own preferences
        if (currentUserId != userId)
        {
            return Forbid();
        }

        // Find user profile
        var profile = await _context.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId.ToString() == userId);

        if (profile == null)
        {
            return NotFound("User profile not found");
        }

        // Find or create preferences
        var prefs = await _context.Set<MatchPreferences>()
            .FirstOrDefaultAsync(p => p.UserProfileId == profile.Id);

        if (prefs == null)
        {
            // Return default preferences if none exist
            return Ok(new GetPreferencesResponse
            {
                MinAge = 18,
                MaxAge = 35,
                MaxDistanceKm = 50,
                PreferredGender = "Any",
                RelationshipGoals = new List<string>(),
                DealBreakerSmoking = false,
                DealBreakerDrinking = false,
                DealBreakerHasChildren = false,
                DealBreakerWantsChildren = false,
                RequireSameReligion = false,
                PreferSimilarEducation = false,
                ShowMeInDiscovery = true,
                MinHeightCm = null,
                MaxHeightCm = null,
                PreferredEthnicities = new List<string>(),
                MustHaveInterests = new List<string>(),
                UpdatedAt = DateTime.UtcNow
            });
        }

        return Ok(new GetPreferencesResponse
        {
            MinAge = prefs.MinAge,
            MaxAge = prefs.MaxAge,
            MaxDistanceKm = prefs.MaxDistanceKm,
            PreferredGender = prefs.PreferredGender,
            RelationshipGoals = ParseJsonArray(prefs.RelationshipGoals),
            DealBreakerSmoking = prefs.DealBreakerSmoking,
            DealBreakerDrinking = prefs.DealBreakerDrinking,
            DealBreakerHasChildren = prefs.DealBreakerHasChildren,
            DealBreakerWantsChildren = prefs.DealBreakerWantsChildren,
            RequireSameReligion = prefs.RequireSameReligion,
            PreferSimilarEducation = prefs.PreferSimilarEducation,
            ShowMeInDiscovery = prefs.ShowMeInDiscovery,
            MinHeightCm = prefs.MinHeightCm,
            MaxHeightCm = prefs.MaxHeightCm,
            PreferredEthnicities = ParseJsonArray(prefs.PreferredEthnicities),
            MustHaveInterests = ParseJsonArray(prefs.MustHaveInterests),
            UpdatedAt = prefs.UpdatedAt
        });
    }

    /// <summary>
    /// Update match preferences
    /// PUT /api/userprofiles/{userId}/preferences
    /// </summary>
    [HttpPut]
    [ProducesResponseType(typeof(GetPreferencesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GetPreferencesResponse>> UpdatePreferences(
        string userId,
        [FromBody] UpdatePreferencesRequest request)
    {
        var currentUserId = GetCurrentUserId();

        // Users can only update their own preferences
        if (currentUserId != userId)
        {
            return Forbid();
        }

        // Validate age range
        if (request.MinAge.HasValue && request.MaxAge.HasValue && request.MinAge > request.MaxAge)
        {
            return BadRequest("MinAge cannot be greater than MaxAge");
        }

        // Validate height range
        if (request.MinHeightCm.HasValue && request.MaxHeightCm.HasValue && request.MinHeightCm > request.MaxHeightCm)
        {
            return BadRequest("MinHeightCm cannot be greater than MaxHeightCm");
        }

        // Find user profile
        var profile = await _context.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId.ToString() == userId);

        if (profile == null)
        {
            return NotFound("User profile not found");
        }

        // Find or create preferences
        var prefs = await _context.Set<MatchPreferences>()
            .FirstOrDefaultAsync(p => p.UserProfileId == profile.Id);

        if (prefs == null)
        {
            prefs = new MatchPreferences
            {
                UserProfileId = profile.Id,
                UserId = profile.UserId,
                CreatedAt = DateTime.UtcNow
            };
            _context.Set<MatchPreferences>().Add(prefs);
        }

        // Update fields if provided
        if (request.MinAge.HasValue) prefs.MinAge = request.MinAge.Value;
        if (request.MaxAge.HasValue) prefs.MaxAge = request.MaxAge.Value;
        if (request.MaxDistanceKm.HasValue) prefs.MaxDistanceKm = request.MaxDistanceKm.Value;
        if (request.PreferredGender != null) prefs.PreferredGender = request.PreferredGender;
        if (request.RelationshipGoals != null)
            prefs.RelationshipGoals = JsonSerializer.Serialize(request.RelationshipGoals);

        if (request.DealBreakerSmoking.HasValue) prefs.DealBreakerSmoking = request.DealBreakerSmoking.Value;
        if (request.DealBreakerDrinking.HasValue) prefs.DealBreakerDrinking = request.DealBreakerDrinking.Value;
        if (request.DealBreakerHasChildren.HasValue) prefs.DealBreakerHasChildren = request.DealBreakerHasChildren.Value;
        if (request.DealBreakerWantsChildren.HasValue) prefs.DealBreakerWantsChildren = request.DealBreakerWantsChildren.Value;

        if (request.RequireSameReligion.HasValue) prefs.RequireSameReligion = request.RequireSameReligion.Value;
        if (request.PreferSimilarEducation.HasValue) prefs.PreferSimilarEducation = request.PreferSimilarEducation.Value;
        if (request.ShowMeInDiscovery.HasValue) prefs.ShowMeInDiscovery = request.ShowMeInDiscovery.Value;

        if (request.MinHeightCm.HasValue) prefs.MinHeightCm = request.MinHeightCm.Value;
        if (request.MaxHeightCm.HasValue) prefs.MaxHeightCm = request.MaxHeightCm.Value;
        if (request.PreferredEthnicities != null)
            prefs.PreferredEthnicities = JsonSerializer.Serialize(request.PreferredEthnicities);
        if (request.MustHaveInterests != null)
            prefs.MustHaveInterests = JsonSerializer.Serialize(request.MustHaveInterests);

        prefs.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated match preferences for user {UserId}", userId);

        // Return updated preferences
        return Ok(new GetPreferencesResponse
        {
            MinAge = prefs.MinAge,
            MaxAge = prefs.MaxAge,
            MaxDistanceKm = prefs.MaxDistanceKm,
            PreferredGender = prefs.PreferredGender,
            RelationshipGoals = ParseJsonArray(prefs.RelationshipGoals),
            DealBreakerSmoking = prefs.DealBreakerSmoking,
            DealBreakerDrinking = prefs.DealBreakerDrinking,
            DealBreakerHasChildren = prefs.DealBreakerHasChildren,
            DealBreakerWantsChildren = prefs.DealBreakerWantsChildren,
            RequireSameReligion = prefs.RequireSameReligion,
            PreferSimilarEducation = prefs.PreferSimilarEducation,
            ShowMeInDiscovery = prefs.ShowMeInDiscovery,
            MinHeightCm = prefs.MinHeightCm,
            MaxHeightCm = prefs.MaxHeightCm,
            PreferredEthnicities = ParseJsonArray(prefs.PreferredEthnicities),
            MustHaveInterests = ParseJsonArray(prefs.MustHaveInterests),
            UpdatedAt = prefs.UpdatedAt
        });
    }

    private static List<string> ParseJsonArray(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json ?? "[]") ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }
}
