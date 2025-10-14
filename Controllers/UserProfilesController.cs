// filepath: UserService/Controllers/UserProfilesController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserService.Data;
using UserService.Models;
using UserService.DTOs;
using UserService.Services;
using System.Text.Json;

namespace UserService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserProfilesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IPhotoService _photoService;
        private readonly IVerificationService _verificationService;
        private readonly ILogger<UserProfilesController> _logger;

        public UserProfilesController(
            ApplicationDbContext context,
            IPhotoService photoService,
            IVerificationService verificationService,
            ILogger<UserProfilesController> logger)
        {
            _context = context;
            _photoService = photoService;
            _verificationService = verificationService;
            _logger = logger;
        }

        /// <summary>
        /// Search and retrieve user profiles with advanced filtering.
        /// </summary>
        [HttpPost("search")]
        public async Task<ActionResult<SearchResultDto<UserProfileSummaryDto>>> SearchUserProfiles([FromBody] SearchUsersDto searchDto)
        {
            try
            {
                var query = _context.UserProfiles.Where(p => p.IsActive);

                // Apply filters
                if (searchDto.MinAge.HasValue)
                {
                    var maxBirthDate = DateTime.Today.AddYears(-searchDto.MinAge.Value);
                    query = query.Where(p => p.DateOfBirth <= maxBirthDate);
                }

                if (searchDto.MaxAge.HasValue)
                {
                    var minBirthDate = DateTime.Today.AddYears(-searchDto.MaxAge.Value - 1);
                    query = query.Where(p => p.DateOfBirth >= minBirthDate);
                }

                if (!string.IsNullOrEmpty(searchDto.Gender))
                {
                    query = query.Where(p => p.Gender == searchDto.Gender);
                }

                if (!string.IsNullOrEmpty(searchDto.Education))
                {
                    query = query.Where(p => p.Education.Contains(searchDto.Education));
                }

                if (!string.IsNullOrEmpty(searchDto.Location))
                {
                    query = query.Where(p => p.City.Contains(searchDto.Location) || 
                                           p.State.Contains(searchDto.Location) || 
                                           p.Country.Contains(searchDto.Location));
                }

                if (searchDto.IsVerified.HasValue)
                {
                    query = query.Where(p => p.IsVerified == searchDto.IsVerified.Value);
                }

                if (searchDto.IsOnline.HasValue)
                {
                    query = query.Where(p => p.IsOnline == searchDto.IsOnline.Value);
                }

                // Apply sorting
                query = searchDto.SortBy.ToLower() switch
                {
                    "age" => searchDto.SortOrder == "asc" ? 
                        query.OrderBy(p => p.DateOfBirth) : 
                        query.OrderByDescending(p => p.DateOfBirth),
                    "verified" => searchDto.SortOrder == "asc" ? 
                        query.OrderBy(p => p.IsVerified) : 
                        query.OrderByDescending(p => p.IsVerified),
                    _ => searchDto.SortOrder == "asc" ? 
                        query.OrderBy(p => p.LastActiveAt) : 
                        query.OrderByDescending(p => p.LastActiveAt)
                };

                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling((double)totalCount / searchDto.PageSize);

                var profiles = await query
                    .Skip((searchDto.Page - 1) * searchDto.PageSize)
                    .Take(searchDto.PageSize)
                    .ToListAsync();

                var results = profiles.Select(p =>
                {
                    var bio = p.Bio ?? string.Empty;
                    var trimmedBio = bio.Length > 150 ? bio.Substring(0, 150) + "..." : bio;

                    return new UserProfileSummaryDto
                    {
                        Id = p.Id,
                        Name = p.Name ?? string.Empty,
                        Age = DateTime.Today.Year - p.DateOfBirth.Year -
                              (p.DateOfBirth.Date > DateTime.Today.AddYears(-(DateTime.Today.Year - p.DateOfBirth.Year)) ? 1 : 0),
                        City = p.City ?? string.Empty,
                        PrimaryPhotoUrl = p.PrimaryPhotoUrl ?? string.Empty,
                        Bio = trimmedBio,
                        Occupation = p.Occupation ?? string.Empty,
                        Interests = JsonSerializer.Deserialize<List<string>>(p.Interests ?? "[]") ?? new List<string>(),
                        IsVerified = p.IsVerified,
                        IsOnline = p.IsOnline,
                        LastActiveAt = p.LastActiveAt
                    };
                }).ToList();

                return Ok(new SearchResultDto<UserProfileSummaryDto>
                {
                    Results = results,
                    TotalCount = totalCount,
                    Page = searchDto.Page,
                    PageSize = searchDto.PageSize,
                    TotalPages = totalPages,
                    HasNext = searchDto.Page < totalPages,
                    HasPrevious = searchDto.Page > 1
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching user profiles");
                return StatusCode(500, "Error searching user profiles");
            }
        }

        /// <summary>
        /// Retrieves a specific user profile by ID with full details.
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<ActionResult<UserProfileDetailDto>> GetUserProfile([FromRoute] int id)
        {
            try
            {
                var userProfile = await _context.UserProfiles.FindAsync(id);

                if (userProfile == null || !userProfile.IsActive)
                {
                    return NotFound();
                }

                var profileDto = new UserProfileDetailDto
                {
                    Id = userProfile.Id,
                    Name = userProfile.Name,
                    Email = userProfile.Email,
                    Bio = userProfile.Bio,
                    Age = userProfile.Age,
                    Gender = userProfile.Gender,
                    Preferences = userProfile.Preferences,
                    SexualOrientation = userProfile.SexualOrientation,
                    City = userProfile.City,
                    State = userProfile.State,
                    Country = userProfile.Country,
                    PhotoUrls = userProfile.PhotoUrlList,
                    PrimaryPhotoUrl = userProfile.PrimaryPhotoUrl,
                    Occupation = userProfile.Occupation,
                    Company = userProfile.Company,
                    Education = userProfile.Education,
                    School = userProfile.School,
                    Height = userProfile.Height,
                    Religion = userProfile.Religion,
                    Ethnicity = userProfile.Ethnicity,
                    SmokingStatus = userProfile.SmokingStatus,
                    DrinkingStatus = userProfile.DrinkingStatus,
                    WantsChildren = userProfile.WantsChildren,
                    HasChildren = userProfile.HasChildren,
                    RelationshipType = userProfile.RelationshipType,
                    Interests = userProfile.InterestsList,
                    Languages = JsonSerializer.Deserialize<List<string>>(userProfile.Languages ?? "[]") ?? new List<string>(),
                    HobbyList = userProfile.HobbyList,
                    InstagramHandle = userProfile.InstagramHandle,
                    SpotifyTopArtists = userProfile.SpotifyTopArtists,
                    IsVerified = userProfile.IsVerified,
                    IsPhoneVerified = userProfile.IsPhoneVerified,
                    IsEmailVerified = userProfile.IsEmailVerified,
                    IsPhotoVerified = userProfile.IsPhotoVerified,
                    IsPremium = userProfile.IsPremium,
                    SubscriptionType = userProfile.SubscriptionType,
                    CreatedAt = userProfile.CreatedAt,
                    LastActiveAt = userProfile.LastActiveAt,
                    IsOnline = userProfile.IsOnline
                };

                return Ok(profileDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving user profile {id}");
                return StatusCode(500, "Error retrieving user profile");
            }
        }

        /// <summary>
        /// Creates a new user profile.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<UserProfileDetailDto>> CreateUserProfile([FromBody] CreateUserProfileDto createDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Check if email already exists
                if (await _context.UserProfiles.AnyAsync(p => p.Email == createDto.Email))
                {
                    return Conflict("Email already exists");
                }

                // Validate age (must be 18+)
                var age = DateTime.Today.Year - createDto.DateOfBirth.Year;
                if (createDto.DateOfBirth.Date > DateTime.Today.AddYears(-age)) age--;
                if (age < 18)
                {
                    return BadRequest("Must be 18 or older");
                }

                var userProfile = new UserProfile
                {
                    Name = createDto.Name,
                    Email = createDto.Email,
                    Bio = createDto.Bio,
                    Gender = createDto.Gender,
                    Preferences = createDto.Preferences,
                    DateOfBirth = createDto.DateOfBirth,
                    City = createDto.City,
                    State = createDto.State,
                    Country = createDto.Country,
                    Latitude = createDto.Latitude,
                    Longitude = createDto.Longitude,
                    Occupation = createDto.Occupation,
                    Education = createDto.Education,
                    Height = createDto.Height,
                    Religion = createDto.Religion,
                    SmokingStatus = createDto.SmokingStatus,
                    DrinkingStatus = createDto.DrinkingStatus,
                    WantsChildren = createDto.WantsChildren,
                    HasChildren = createDto.HasChildren,
                    RelationshipType = createDto.RelationshipType,
                    Interests = JsonSerializer.Serialize(createDto.Interests),
                    Languages = JsonSerializer.Serialize(createDto.Languages),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    LastActiveAt = DateTime.UtcNow,
                    IsActive = true,
                    IsOnline = true
                };

                _context.UserProfiles.Add(userProfile);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Created new user profile with ID {userProfile.Id}");

                return CreatedAtAction(nameof(GetUserProfile), new { id = userProfile.Id }, 
                    await GetUserProfile(userProfile.Id));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user profile");
                return StatusCode(500, "Error creating user profile");
            }
        }

        /// <summary>
        /// Updates a user profile.
        /// </summary>
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateUserProfile(int id, [FromBody] UpdateUserProfileDto updateDto)
        {
            try
            {
                var userProfile = await _context.UserProfiles.FindAsync(id);
                if (userProfile == null)
                {
                    return NotFound();
                }

                // Update only provided fields
                if (!string.IsNullOrEmpty(updateDto.Name))
                    userProfile.Name = updateDto.Name;
                if (!string.IsNullOrEmpty(updateDto.Bio))
                    userProfile.Bio = updateDto.Bio;
                if (!string.IsNullOrEmpty(updateDto.Occupation))
                    userProfile.Occupation = updateDto.Occupation;
                if (!string.IsNullOrEmpty(updateDto.Company))
                    userProfile.Company = updateDto.Company;
                if (!string.IsNullOrEmpty(updateDto.Education))
                    userProfile.Education = updateDto.Education;
                if (!string.IsNullOrEmpty(updateDto.School))
                    userProfile.School = updateDto.School;
                if (updateDto.Height.HasValue)
                    userProfile.Height = updateDto.Height.Value;
                if (!string.IsNullOrEmpty(updateDto.Religion))
                    userProfile.Religion = updateDto.Religion;
                if (!string.IsNullOrEmpty(updateDto.Ethnicity))
                    userProfile.Ethnicity = updateDto.Ethnicity;
                if (!string.IsNullOrEmpty(updateDto.SmokingStatus))
                    userProfile.SmokingStatus = updateDto.SmokingStatus;
                if (!string.IsNullOrEmpty(updateDto.DrinkingStatus))
                    userProfile.DrinkingStatus = updateDto.DrinkingStatus;
                if (updateDto.WantsChildren.HasValue)
                    userProfile.WantsChildren = updateDto.WantsChildren.Value;
                if (updateDto.HasChildren.HasValue)
                    userProfile.HasChildren = updateDto.HasChildren.Value;
                if (!string.IsNullOrEmpty(updateDto.RelationshipType))
                    userProfile.RelationshipType = updateDto.RelationshipType;
                if (updateDto.Interests != null)
                    userProfile.Interests = JsonSerializer.Serialize(updateDto.Interests);
                if (updateDto.Languages != null)
                    userProfile.Languages = JsonSerializer.Serialize(updateDto.Languages);
                if (!string.IsNullOrEmpty(updateDto.HobbyList))
                    userProfile.HobbyList = updateDto.HobbyList;
                if (!string.IsNullOrEmpty(updateDto.InstagramHandle))
                    userProfile.InstagramHandle = updateDto.InstagramHandle;

                userProfile.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Updated user profile {id}");

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating user profile {id}");
                return StatusCode(500, "Error updating user profile");
            }
        }

        /// <summary>
        /// Updates user's location.
        /// </summary>
        [HttpPut("{id:int}/location")]
        public async Task<IActionResult> UpdateLocation(int id, [FromBody] LocationUpdateDto locationDto)
        {
            try
            {
                var userProfile = await _context.UserProfiles.FindAsync(id);
                if (userProfile == null)
                {
                    return NotFound();
                }

                userProfile.Latitude = locationDto.Latitude;
                userProfile.Longitude = locationDto.Longitude;
                userProfile.City = locationDto.City;
                userProfile.State = locationDto.State;
                userProfile.Country = locationDto.Country;
                userProfile.LastLocationUpdate = DateTime.UtcNow;
                userProfile.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Updated location for user {id}");

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating location for user {id}");
                return StatusCode(500, "Error updating location");
            }
        }

        /// <summary>
        /// Updates user's privacy settings.
        /// </summary>
        [HttpPut("{id:int}/privacy")]
        public async Task<IActionResult> UpdatePrivacySettings(int id, [FromBody] PrivacySettingsDto privacyDto)
        {
            try
            {
                var userProfile = await _context.UserProfiles.FindAsync(id);
                if (userProfile == null)
                {
                    return NotFound();
                }

                userProfile.ShowAge = privacyDto.ShowAge;
                userProfile.ShowDistance = privacyDto.ShowDistance;
                userProfile.ShowOnlineStatus = privacyDto.ShowOnlineStatus;
                userProfile.AllowMessageFromMatches = privacyDto.AllowMessageFromMatches;
                userProfile.AllowMessageFromEveryone = privacyDto.AllowMessageFromEveryone;
                userProfile.IsPrivate = privacyDto.IsPrivate;
                userProfile.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Updated privacy settings for user {id}");

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating privacy settings for user {id}");
                return StatusCode(500, "Error updating privacy settings");
            }
        }

        /// <summary>
        /// Uploads a photo for the user.
        /// </summary>
        [HttpPost("{id:int}/photos")]
        public async Task<ActionResult<PhotoResponseDto>> UploadPhoto(int id, [FromForm] PhotoUploadDto photoDto)
        {
            try
            {
                var userProfile = await _context.UserProfiles.FindAsync(id);
                if (userProfile == null)
                {
                    return NotFound();
                }

                var photoResponse = await _photoService.UploadPhotoAsync(id, photoDto);

                // Update user profile with photo information
                var currentPhotos = userProfile.PhotoUrlList;
                currentPhotos.Add(photoResponse.PhotoUrl);
                userProfile.PhotoUrls = JsonSerializer.Serialize(currentPhotos);
                userProfile.PhotoCount = currentPhotos.Count;

                if (photoDto.IsPrimary || string.IsNullOrEmpty(userProfile.PrimaryPhotoUrl))
                {
                    userProfile.PrimaryPhotoUrl = photoResponse.PhotoUrl;
                }

                userProfile.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Photo uploaded for user {id}");

                return Ok(photoResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error uploading photo for user {id}");
                return StatusCode(500, "Error uploading photo");
            }
        }

        /// <summary>
        /// Gets all photos for a user.
        /// </summary>
        [HttpGet("{id:int}/photos")]
        public async Task<ActionResult<List<PhotoResponseDto>>> GetUserPhotos(int id)
        {
            try
            {
                var userProfile = await _context.UserProfiles.FindAsync(id);
                if (userProfile == null)
                {
                    return NotFound();
                }

                var photos = await _photoService.GetUserPhotosAsync(id);
                return Ok(photos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting photos for user {id}");
                return StatusCode(500, "Error getting photos");
            }
        }

        /// <summary>
        /// Deletes a photo.
        /// </summary>
        [HttpDelete("{id:int}/photos")]
        public async Task<IActionResult> DeletePhoto(int id, [FromQuery] string photoUrl)
        {
            try
            {
                var userProfile = await _context.UserProfiles.FindAsync(id);
                if (userProfile == null)
                {
                    return NotFound();
                }

                var success = await _photoService.DeletePhotoAsync(id, photoUrl);
                if (!success)
                {
                    return BadRequest("Failed to delete photo");
                }

                // Update user profile
                var currentPhotos = userProfile.PhotoUrlList;
                currentPhotos.Remove(photoUrl);
                userProfile.PhotoUrls = JsonSerializer.Serialize(currentPhotos);
                userProfile.PhotoCount = currentPhotos.Count;

                if (userProfile.PrimaryPhotoUrl == photoUrl)
                {
                    userProfile.PrimaryPhotoUrl = currentPhotos.FirstOrDefault() ?? "";
                }

                userProfile.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Photo deleted for user {id}");

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting photo for user {id}");
                return StatusCode(500, "Error deleting photo");
            }
        }

        /// <summary>
        /// Requests verification for a user.
        /// </summary>
        [HttpPost("{id:int}/verification")]
        public async Task<IActionResult> RequestVerification(int id, [FromForm] VerificationRequestDto verificationDto)
        {
            try
            {
                bool success = verificationDto.VerificationType.ToLower() switch
                {
                    "photo" => await _verificationService.RequestPhotoVerificationAsync(id, verificationDto),
                    "email" => await _verificationService.RequestEmailVerificationAsync(id),
                    _ => false
                };

                if (!success)
                {
                    return BadRequest("Failed to request verification");
                }

                return Ok(new { Message = "Verification request submitted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error requesting verification for user {id}");
                return StatusCode(500, "Error requesting verification");
            }
        }

        /// <summary>
        /// Gets verification status for a user.
        /// </summary>
        [HttpGet("{id:int}/verification")]
        public async Task<ActionResult<Dictionary<string, bool>>> GetVerificationStatus(int id)
        {
            try
            {
                var status = await _verificationService.GetVerificationStatusAsync(id);
                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting verification status for user {id}");
                return StatusCode(500, "Error getting verification status");
            }
        }

        /// <summary>
        /// Updates user's online status.
        /// </summary>
        [HttpPut("{id:int}/online-status")]
        public async Task<IActionResult> UpdateOnlineStatus(int id, [FromBody] bool isOnline)
        {
            try
            {
                var userProfile = await _context.UserProfiles.FindAsync(id);
                if (userProfile == null)
                {
                    return NotFound();
                }

                userProfile.IsOnline = isOnline;
                userProfile.LastActiveAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating online status for user {id}");
                return StatusCode(500, "Error updating online status");
            }
        }

        /// <summary>
        /// Deactivates a user profile.
        /// </summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeactivateUserProfile(int id)
        {
            try
            {
                var userProfile = await _context.UserProfiles.FindAsync(id);
                if (userProfile == null)
                {
                    return NotFound();
                }

                userProfile.IsActive = false;
                userProfile.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Deactivated user profile {id}");

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deactivating user profile {id}");
                return StatusCode(500, "Error deactivating user profile");
            }
        }

        /// <summary>
        /// Health check endpoint.
        /// </summary>
        [HttpGet("health")]
        public IActionResult HealthCheck()
        {
            try
            {
                var dbConnected = _context.Database.CanConnect();
                return Ok(new
                {
                    Status = "Healthy",
                    Timestamp = DateTime.UtcNow,
                    DatabaseConnected = dbConnected,
                    Service = "UserService v1.0"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed");
                return StatusCode(500, new
                {
                    Status = "Unhealthy",
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Debug endpoint to show current user's authentication status and claims.
        /// </summary>
        [HttpGet("debug-auth")]
        public IActionResult DebugAuth()
        {
            return Ok(new
            {
                IsAuthenticated = User.Identity?.IsAuthenticated,
                AuthenticationType = User.Identity?.AuthenticationType,
                Claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList()
            });
        }
    }
}