using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using UserService.Common;
using UserService.Data;
using UserService.DTOs;
using UserService.Services;

namespace UserService.Controllers
{
    [Route("api/profiles")]
    [ApiController]
    [Authorize]
    public class ProfileController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ProfileController> _logger;
        private readonly IProfileCompletenessService _completenessService;

        public ProfileController(
            ApplicationDbContext context,
            ILogger<ProfileController> logger,
            IProfileCompletenessService completenessService)
        {
            _context = context;
            _logger = logger;
            _completenessService = completenessService;
        }

        /// <summary>
        /// Get the authenticated user's profile using JWT "sub" claim (Keycloak username).
        /// </summary>
        [HttpGet("me")]
        public async Task<ActionResult<ApiResponse<UserProfileDetailDto>>> GetMyProfile()
        {
            try
            {
                // Get the "sub" claim from JWT (Keycloak user ID)
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst("sub")?.Value;

                if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                {
                    _logger.LogWarning("Profile request with no valid sub claim in JWT");
                    return Unauthorized(ApiResponse<UserProfileDetailDto>.FailureResult(
                        "Invalid authentication token", "INVALID_TOKEN"));
                }

                _logger.LogInformation($"Looking up profile for user ID: {userId}");

                // Find profile by UserId
                var profile = await _context.UserProfiles
                    .FirstOrDefaultAsync(p => p.UserId == userId);

                if (profile == null)
                {
                    _logger.LogWarning($"No profile found for user ID: {userId}");
                    return NotFound(ApiResponse<UserProfileDetailDto>.FailureResult(
                        "Profile not found for authenticated user", "PROFILE_NOT_FOUND"));
                }

                // Deserialize JSON fields
                var interests = string.IsNullOrEmpty(profile.Interests)
                    ? new List<string>()
                    : JsonSerializer.Deserialize<List<string>>(profile.Interests) ?? new List<string>();

                var languages = string.IsNullOrEmpty(profile.Languages)
                    ? new List<string>()
                    : JsonSerializer.Deserialize<List<string>>(profile.Languages) ?? new List<string>();

                var dto = new UserProfileDetailDto
                {
                    Id = profile.Id,
                    Name = profile.Name,
                    Email = profile.Email,
                    Bio = profile.Bio,
                    Gender = profile.Gender,
                    Preferences = profile.Preferences,
                    SexualOrientation = profile.SexualOrientation ?? string.Empty,
                    Age = DateTime.UtcNow.Year - profile.DateOfBirth.Year,
                    City = profile.City,
                    State = profile.State,
                    Country = profile.Country,
                    Occupation = profile.Occupation,
                    Company = profile.Company,
                    Education = profile.Education,
                    School = profile.School,
                    Interests = interests,
                    Languages = languages,
                    HobbyList = profile.HobbyList,
                    Height = profile.Height,
                    Religion = profile.Religion,
                    Ethnicity = profile.Ethnicity,
                    SmokingStatus = profile.SmokingStatus,
                    DrinkingStatus = profile.DrinkingStatus,
                    WantsChildren = profile.WantsChildren,
                    HasChildren = profile.HasChildren,
                    RelationshipType = profile.RelationshipType,
                    PrimaryPhotoUrl = profile.PrimaryPhotoUrl,
                    PhotoUrls = profile.PhotoUrlList,
                    InstagramHandle = profile.InstagramHandle,
                    SpotifyTopArtists = profile.SpotifyTopArtists,
                    IsVerified = profile.IsVerified,
                    IsOnline = profile.IsOnline,
                    LastActiveAt = profile.LastActiveAt,
                    CreatedAt = profile.CreatedAt,
                    OnboardingStatus = profile.OnboardingStatus
                };

                return Ok(ApiResponse<UserProfileDetailDto>.SuccessResult(dto));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving profile for authenticated user");
                return StatusCode(500, ApiResponse<UserProfileDetailDto>.FailureResult(
                    "Error retrieving profile", "INTERNAL_ERROR"));
            }
        }

        /// <summary>
        /// Update the authenticated user's profile.
        /// </summary>
        [HttpPut("me")]
        public async Task<ActionResult<ApiResponse<UserProfileDetailDto>>> UpdateMyProfile(
            [FromBody] UpdateProfileDto updates)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst("sub")?.Value;

                if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
                {
                    return Unauthorized(ApiResponse<UserProfileDetailDto>.FailureResult(
                        "Invalid authentication token", "INVALID_TOKEN"));
                }

                var profile = await _context.UserProfiles
                    .FirstOrDefaultAsync(p => p.UserId == userGuid);

                if (profile == null)
                {
                    return NotFound(ApiResponse<UserProfileDetailDto>.FailureResult(
                        "Profile not found", "PROFILE_NOT_FOUND"));
                }

                // Update only provided fields
                if (updates.Bio != null) profile.Bio = updates.Bio;
                if (updates.Gender != null) profile.Gender = updates.Gender;
                if (updates.City != null) profile.City = updates.City;
                if (updates.Occupation != null) profile.Occupation = updates.Occupation;

                await _context.SaveChangesAsync();

                // Return updated profile (reuse GET logic)
                return await GetMyProfile();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile");
                return StatusCode(500, ApiResponse<UserProfileDetailDto>.FailureResult(
                    "Error updating profile", "INTERNAL_ERROR"));
            }
        }

        /// <summary>
        /// Debug endpoint to show authentication claims.
        /// </summary>
        [HttpGet("debug/claims")]
        public IActionResult GetClaims()
        {
            return Ok(new
            {
                IsAuthenticated = User.Identity?.IsAuthenticated,
                AuthenticationType = User.Identity?.AuthenticationType,
                Name = User.Identity?.Name,
                Claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList()
            });
        }

        /// <summary>
        /// Get profile completeness score for the authenticated user.
        /// 3-tier weighted formula: Required (40%), Encouraged (35%), Optional (25%).
        /// </summary>
        [HttpGet("me/completeness")]
        public async Task<ActionResult<ApiResponse<ProfileCompletenessDto>>> GetProfileCompleteness()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                  ?? User.FindFirst("sub")?.Value;

                if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                {
                    return Unauthorized(ApiResponse<ProfileCompletenessDto>.FailureResult(
                        "Invalid authentication token", "INVALID_TOKEN"));
                }

                var profile = await _context.UserProfiles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.UserId == userId);

                if (profile == null)
                {
                    return NotFound(ApiResponse<ProfileCompletenessDto>.FailureResult(
                        "Profile not found", "NOT_FOUND"));
                }

                var result = _completenessService.Calculate(profile);

                var dto = new ProfileCompletenessDto(
                    result.Percentage,
                    result.FilledFields.Select(f => new FieldStatusDto(f.FieldName, f.IsFilled, f.Weight, f.Tier)).ToList(),
                    result.MissingFields.Select(f => new FieldStatusDto(f.FieldName, f.IsFilled, f.Weight, f.Tier)).ToList(),
                    result.NextSuggestion
                );

                return Ok(ApiResponse<ProfileCompletenessDto>.SuccessResult(dto));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating profile completeness");
                return StatusCode(500, ApiResponse<ProfileCompletenessDto>.FailureResult(
                    "Error calculating profile completeness", "INTERNAL_ERROR"));
            }
        }

    }
}
