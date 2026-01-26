using Microsoft.EntityFrameworkCore;
using UserService.Data;
using UserService.DTOs;
using UserService.Models;

namespace UserService.Services
{
    public class AccountDeletionService : IAccountDeletionService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<AccountDeletionService> _logger;
        private readonly IConfiguration _configuration;

        public AccountDeletionService(
            ApplicationDbContext context,
            IHttpClientFactory httpClientFactory,
            ILogger<AccountDeletionService> logger,
            IConfiguration configuration)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<AccountDeletionResult> DeleteAccountAsync(int userProfileId, bool hardDelete = false, string? reason = null)
        {
            var summary = new AccountDeletionSummary
            {
                DeletedAt = DateTime.UtcNow
            };

            try
            {
                var userProfile = await _context.UserProfiles.FindAsync(userProfileId);
                if (userProfile == null)
                {
                    return new AccountDeletionResult
                    {
                        Success = false,
                        Message = "User profile not found",
                        Summary = summary
                    };
                }

                var userId = userProfile.UserId;
                var gatewayBaseUrl = _configuration["Gateway:BaseUrl"] ?? "http://dejting-yarp:8080";
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.BaseAddress = new Uri(gatewayBaseUrl);

                // 1. Delete from PhotoService
                summary.PhotosDeleted = await DeleteUserPhotosAsync(httpClient, userProfileId);

                // 2. Delete from MatchmakingService (matches)
                summary.MatchesDeleted = await DeleteUserMatchesAsync(httpClient, userProfileId);

                // 3. Delete from MessagingService (messages) - uses Keycloak userId
                summary.MessagesDeleted = await DeleteUserMessagesAsync(httpClient, userId.ToString());

                // 4. Delete from SwipeService (swipes)
                summary.SwipesDeleted = await DeleteUserSwipesAsync(httpClient, userProfileId);

                // 5. Delete from SafetyService (reports and blocks) - uses Keycloak userId
                var (reports, blocks) = await DeleteUserSafetyDataAsync(httpClient, userId.ToString());
                summary.SafetyReportsDeleted = reports;
                summary.BlocksDeleted = blocks;

                // 6. Delete user preferences
                await DeleteUserPreferencesAsync(userProfileId);

                // 7. Delete or deactivate the user profile
                if (hardDelete)
                {
                    _context.UserProfiles.Remove(userProfile);
                    summary.ProfileDeleted = true;
                    _logger.LogWarning("Hard deleted user profile {UserProfileId}. Reason: {Reason}", userProfileId, reason ?? "Not specified");
                }
                else
                {
                    userProfile.IsActive = false;
                    userProfile.UpdatedAt = DateTime.UtcNow;
                    userProfile.Email = $"deleted_{userId}_{DateTime.UtcNow.Ticks}@deleted.local";
                    userProfile.Bio = "";
                    userProfile.Name = "[Deleted User]";
                    summary.ProfileDeleted = true;
                    _logger.LogInformation("Soft deleted (deactivated) user profile {UserProfileId}. Reason: {Reason}", userProfileId, reason ?? "Not specified");
                }

                await _context.SaveChangesAsync();

                return new AccountDeletionResult
                {
                    Success = true,
                    Message = hardDelete ? "Account permanently deleted" : "Account deactivated and data removed",
                    Summary = summary
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting account for user profile {UserProfileId}", userProfileId);
                summary.Errors.Add($"Error: {ex.Message}");
                
                return new AccountDeletionResult
                {
                    Success = false,
                    Message = "Error occurred during account deletion",
                    Summary = summary
                };
            }
        }

        private async Task<int> DeleteUserPhotosAsync(HttpClient httpClient, int userProfileId)
        {
            try
            {
                var response = await httpClient.DeleteAsync($"/api/photos/user/{userProfileId}");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    if (int.TryParse(content, out var count))
                    {
                        _logger.LogInformation("Deleted {Count} photos for user {UserProfileId}", count, userProfileId);
                        return count;
                    }
                }
                else
                {
                    _logger.LogWarning("Failed to delete photos for user {UserProfileId}: {StatusCode}", userProfileId, response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting photos for user {UserProfileId}", userProfileId);
            }
            return 0;
        }

        private async Task<int> DeleteUserMatchesAsync(HttpClient httpClient, int userProfileId)
        {
            try
            {
                var response = await httpClient.DeleteAsync($"/api/matchmaking/user/{userProfileId}/matches");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    if (int.TryParse(content, out var count))
                    {
                        _logger.LogInformation("Deleted {Count} matches for user {UserProfileId}", count, userProfileId);
                        return count;
                    }
                }
                else
                {
                    _logger.LogWarning("Failed to delete matches for user {UserProfileId}: {StatusCode}", userProfileId, response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting matches for user {UserProfileId}", userProfileId);
            }
            return 0;
        }

        private async Task<int> DeleteUserMessagesAsync(HttpClient httpClient, string userId)
        {
            try
            {
                var response = await httpClient.DeleteAsync($"/api/messages/user/{userId}");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    if (int.TryParse(content, out var count))
                    {
                        _logger.LogInformation("Deleted {Count} messages for user {UserId}", count, userId);
                        return count;
                    }
                }
                else
                {
                    _logger.LogWarning("Failed to delete messages for user {UserId}: {StatusCode}", userId, response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting messages for user {UserId}", userId);
            }
            return 0;
        }

        private async Task<int> DeleteUserSwipesAsync(HttpClient httpClient, int userProfileId)
        {
            try
            {
                var response = await httpClient.DeleteAsync($"/api/swipes/user/{userProfileId}");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    if (int.TryParse(content, out var count))
                    {
                        _logger.LogInformation("Deleted {Count} swipes for user {UserProfileId}", count, userProfileId);
                        return count;
                    }
                }
                else
                {
                    _logger.LogWarning("Failed to delete swipes for user {UserProfileId}: {StatusCode}", userProfileId, response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting swipes for user {UserProfileId}", userProfileId);
            }
            return 0;
        }

        private async Task<(int reports, int blocks)> DeleteUserSafetyDataAsync(HttpClient httpClient, string userId)
        {
            int reports = 0, blocks = 0;
            
            try
            {
                var response = await httpClient.DeleteAsync($"/api/safety/user/{userId}");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var parts = content.Split(',');
                    if (parts.Length == 2)
                    {
                        int.TryParse(parts[0], out reports);
                        int.TryParse(parts[1], out blocks);
                        _logger.LogInformation("Deleted {Reports} reports and {Blocks} blocks for user {UserId}", 
                            reports, blocks, userId);
                    }
                }
                else
                {
                    _logger.LogWarning("Failed to delete safety data for user {UserId}: {StatusCode}", userId, response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting safety data for user {UserId}", userId);
            }
            
            return (reports, blocks);
        }

        private async Task DeleteUserPreferencesAsync(int userProfileId)
        {
            try
            {
                var preferences = await _context.MatchPreferences
                    .FirstOrDefaultAsync(p => p.UserProfileId == userProfileId);
                
                if (preferences != null)
                {
                    _context.MatchPreferences.Remove(preferences);
                    _logger.LogInformation("Deleted preferences for user {UserProfileId}", userProfileId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting preferences for user {UserProfileId}", userProfileId);
            }
        }
    }
}
