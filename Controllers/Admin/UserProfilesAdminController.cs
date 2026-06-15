using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserService.Common;
using UserService.Data;
using UserService.DTOs;

namespace UserService.Controllers.Admin
{
    [Route("api/admin/userprofiles")]
    [ApiController]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class UserProfilesAdminController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UserProfilesAdminController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _env;

        public UserProfilesAdminController(
            ApplicationDbContext context,
            ILogger<UserProfilesAdminController> logger,
            IConfiguration configuration,
            IWebHostEnvironment env)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
            _env = env;
        }

        [HttpPut("{id:int}/bind-keycloak")]
        public async Task<ActionResult<ApiResponse<UserProfileDetailDto>>> BindKeycloak(int id, [FromBody] BindKeycloakDto dto)
        {
            if (!_env.IsDevelopment())
            {
                _logger.LogWarning("Admin bind-keycloak called outside Development");
                return NotFound();
            }

            var remoteIp = HttpContext.Connection.RemoteIpAddress;
            if (remoteIp == null || !IPAddress.IsLoopback(remoteIp))
            {
                _logger.LogWarning("Admin bind-keycloak called from non-localhost IP {Ip}", remoteIp);
                return NotFound();
            }

            if (!string.IsNullOrEmpty(Request.Headers["X-Forwarded-For"].FirstOrDefault()))
            {
                return NotFound();
            }

            var header = Request.Headers["X-Internal-Secret"].FirstOrDefault();
            var expected = _configuration["InternalApiSecret"];
            if (string.IsNullOrEmpty(expected) || header != expected)
            {
                _logger.LogWarning("Unauthorized internal bind attempt for profile {ProfileId}", id);
                return Unauthorized(ApiResponse<UserProfileDetailDto>.FailureResult("Unauthorized"));
            }

            if (dto == null || string.IsNullOrEmpty(dto.KeycloakId) || !Guid.TryParse(dto.KeycloakId, out var newGuid))
            {
                return BadRequest(ApiResponse<UserProfileDetailDto>.FailureResult("Invalid KeycloakId"));
            }

            var profile = await _context.UserProfiles.FindAsync(id);
            if (profile == null)
            {
                return NotFound(ApiResponse<UserProfileDetailDto>.FailureResult("Profile not found"));
            }

            var conflict = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == newGuid && p.Id != id);
            if (conflict != null)
            {
                return Conflict(ApiResponse<UserProfileDetailDto>.FailureResult("Another profile already bound to this KeycloakId"));
            }

            profile.UserId = newGuid;
            profile.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var interests = string.IsNullOrEmpty(profile.Interests)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(profile.Interests) ?? new List<string>();
            var languages = string.IsNullOrEmpty(profile.Languages)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(profile.Languages) ?? new List<string>();

            var dtoOut = new UserProfileDetailDto
            {
                Id = profile.Id,
                KeycloakId = profile.UserId.ToString(),
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

            _logger.LogInformation("Bound profile {ProfileId} to KeycloakId {KeycloakId}", id, dto.KeycloakId);
            return Ok(ApiResponse<UserProfileDetailDto>.SuccessResult(dtoOut, "Profile bound to KeycloakId"));
        }
    }

    public class BindKeycloakDto
    {
        public string KeycloakId { get; set; } = string.Empty;
    }
}
