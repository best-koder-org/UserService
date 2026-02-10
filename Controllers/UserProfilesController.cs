// filepath: UserService/Controllers/UserProfilesController.cs
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserService.Commands;
using UserService.Common;
using UserService.Data;
using UserService.Models;
using UserService.DTOs;
using UserService.Queries;
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
        private readonly IAccountDeletionService _accountDeletionService;
        private readonly ILogger<UserProfilesController> _logger;
        private readonly IMediator _mediator;

        public UserProfilesController(
            ApplicationDbContext context,
            IPhotoService photoService,
            IVerificationService verificationService,
            IAccountDeletionService accountDeletionService,
            ILogger<UserProfilesController> logger,
            IMediator mediator)
        {
            _context = context;
            _photoService = photoService;
            _verificationService = verificationService;
            _accountDeletionService = accountDeletionService;
            _logger = logger;
            _mediator = mediator;
        }

        /// <summary>
        /// Search and retrieve user profiles with advanced filtering.
        /// </summary>
        [HttpPost("search")]
        public async Task<ActionResult<ApiResponse<SearchResultDto<UserProfileSummaryDto>>>> SearchUserProfiles([FromBody] SearchUsersDto searchDto)
        {
            var query = new SearchUserProfilesQuery
            {
                MinAge = searchDto.MinAge,
                MaxAge = searchDto.MaxAge,
                Gender = searchDto.Gender,
                Education = searchDto.Education,
                Location = searchDto.Location,
                IsVerified = searchDto.IsVerified,
                IsOnline = searchDto.IsOnline,
                SortBy = searchDto.SortBy,
                SortOrder = searchDto.SortOrder,
                Page = searchDto.Page,
                PageSize = searchDto.PageSize
            };

            var result = await _mediator.Send(query);

            if (result.IsFailure)
            {
                return StatusCode(500, ApiResponse<SearchResultDto<UserProfileSummaryDto>>.FailureResult(
                    result.Error ?? "Error searching user profiles"));
            }

            return Ok(ApiResponse<SearchResultDto<UserProfileSummaryDto>>.SuccessResult(result.Value!));
        }

        /// <summary>
        /// Retrieves a specific user profile by ID with full details.
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<UserProfileDetailDto>>> GetUserProfile([FromRoute] int id)
        {
            var query = new GetUserProfileQuery(id);
            var result = await _mediator.Send(query);

            if (result.IsFailure)
            {
                return NotFound(ApiResponse<UserProfileDetailDto>.FailureResult(
                    result.Error ?? "User profile not found", "NOT_FOUND"));
            }

            return Ok(ApiResponse<UserProfileDetailDto>.SuccessResult(result.Value!));
        }

        /// <summary>
        /// Creates a new user profile.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ApiResponse<UserProfileDetailDto>>> CreateUserProfile([FromBody] CreateUserProfileDto createDto)
        {
            var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var keycloakUserId = !string.IsNullOrEmpty(userIdClaim) ? Guid.Parse(userIdClaim) : Guid.Empty;
            var command = new CreateUserProfileCommand
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
                Interests = createDto.Interests,
                Languages = createDto.Languages,
                Height = createDto.Height,
                Religion = createDto.Religion,
                SmokingStatus = createDto.SmokingStatus,
                DrinkingStatus = createDto.DrinkingStatus,
                WantsChildren = createDto.WantsChildren,
                HasChildren = createDto.HasChildren,
                RelationshipType = createDto.RelationshipType,
                UserId = keycloakUserId
            };

            var result = await _mediator.Send(command);

            if (result.IsFailure)
            {
                if (result.Error?.Contains("Email already exists") == true)
                {
                    return Conflict(ApiResponse<UserProfileDetailDto>.FailureResult(
                        "Email already exists", "EMAIL_EXISTS"));
                }

                if (result.Error?.Contains("18 or older") == true)
                {
                    return BadRequest(ApiResponse<UserProfileDetailDto>.FailureResult(
                        "Must be 18 or older", "AGE_REQUIREMENT"));
                }

                return BadRequest(ApiResponse<UserProfileDetailDto>.FailureResult(
                    result.Error ?? "Failed to create user profile"));
            }

            return CreatedAtAction(
                nameof(GetUserProfile),
                new { id = result.Value!.Id },
                ApiResponse<UserProfileDetailDto>.SuccessResult(
                    result.Value,
                    "User profile created successfully"));
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
        /// Deletes (or deactivates) a user account and all associated data across services.
        /// </summary>
        /// <param name="id">User profile ID to delete</param>
        /// <param name="request">Deletion request with options and confirmation</param>
        /// <returns>Summary of deletion operations</returns>
        [HttpDelete("{id:int}")]
        [Authorize]
        public async Task<ActionResult<AccountDeletionResult>> DeleteAccount(int id, [FromBody] AccountDeletionRequest? request = null)
        {
            try
            {
                // Authorization: User can only delete their own account
                var userProfile = await _context.UserProfiles.FindAsync(id);
                if (userProfile == null)
                {
                    return NotFound(new AccountDeletionResult
                    {
                        Success = false,
                        Message = "User profile not found",
                        Summary = new AccountDeletionSummary()
                    });
                }

                // Get user ID from JWT token
                var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst("userId")?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || userProfile.UserId.ToString() != userIdClaim)
                {
                    _logger.LogWarning("Unauthorized account deletion attempt. User {Requester} tried to delete profile {ProfileId} owned by {Owner}",
                        userIdClaim, id, userProfile.UserId);
                    return Forbid();
                }

                // Execute deletion
                var hardDelete = request?.HardDelete ?? false;
                var reason = request?.Reason;
                
                var result = await _accountDeletionService.DeleteAccountAsync(id, hardDelete, reason);

                if (result.Success)
                {
                    _logger.LogInformation("Account deletion completed for user {UserProfileId}. Hard delete: {HardDelete}. Summary: {@Summary}",
                        id, hardDelete, result.Summary);
                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning("Account deletion failed for user {UserProfileId}. Message: {Message}",
                        id, result.Message);
                    return StatusCode(500, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting account for user profile {UserProfileId}", id);
                return StatusCode(500, new AccountDeletionResult
                {
                    Success = false,
                    Message = "Unexpected error during account deletion",
                    Summary = new AccountDeletionSummary
                    {
                        Errors = new List<string> { ex.Message }
                    }
                });
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