using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using UserService.Common;
using UserService.Data;
using UserService.DTOs;

namespace UserService.Controllers
{
    [Route("[controller]")]
    [ApiController]
    [Authorize]
    public class ProfileController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ProfileController> _logger;

        public ProfileController(
            ApplicationDbContext context,
            ILogger<ProfileController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Get the authenticated user's profile using JWT "sub" claim (Keycloak username).
        /// </summary>
        [HttpGet("me")]
        public async Task<ActionResult<ApiResponse<UserProfileDetailDto>>> GetMyProfile()
        {
            try
            {
                // Get the "sub" claim from JWT (Keycloak username like "erik_astrom")
                var username = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst("sub")?.Value;

                if (string.IsNullOrEmpty(username))
                {
                    _logger.LogWarning("Profile request with no sub claim in JWT");
                    return Unauthorized(ApiResponse<UserProfileDetailDto>.FailureResult(
                        "Invalid authentication token", "INVALID_TOKEN"));
                }

                _logger.LogInformation($"Looking up profile for username: {username}");

                // Find profile by username (email or keycloak username)
                var profile = await _context.UserProfiles
                    .FirstOrDefaultAsync(p => p.Email.ToLower() == username.ToLower() 
                        || p.Name.ToLower().Contains(username.ToLower().Replace("_", " ")));

                if (profile == null)
                {
                    _logger.LogWarning($"No profile found for username: {username}. Available profiles: {string.Join(", ", _context.UserProfiles.Select(p => $"{p.Name}({p.Email})").Take(10))}");
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
    }
}
