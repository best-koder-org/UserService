using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using UserService.Common;
using UserService.Data;

namespace UserService.Controllers
{
    [Route("api/users")]
    [ApiController]
    [Authorize]
    public class DeviceTokenController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DeviceTokenController> _logger;

        public DeviceTokenController(
            ApplicationDbContext context,
            ILogger<DeviceTokenController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Register or update the device's FCM push notification token.
        /// Called by the Flutter client after login and on token refresh.
        /// </summary>
        [HttpPut("device-token")]
        public async Task<IActionResult> UpdateDeviceToken([FromBody] DeviceTokenRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(ApiResponse<object>.FailureResult(
                    "Invalid authentication token", "INVALID_TOKEN"));
            }

            var profile = await _context.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null)
            {
                return NotFound(ApiResponse<object>.FailureResult(
                    "Profile not found", "PROFILE_NOT_FOUND"));
            }

            profile.FcmToken = request.Token;
            profile.FcmPlatform = request.Platform;
            profile.FcmTokenUpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("FCM token updated for user {UserId}, platform={Platform}",
                userId, request.Platform);

            return Ok(ApiResponse<object>.SuccessResult(new { registered = true }));
        }

        /// <summary>
        /// Remove the device token (e.g. on logout).
        /// </summary>
        [HttpDelete("device-token")]
        public async Task<IActionResult> RemoveDeviceToken()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(ApiResponse<object>.FailureResult(
                    "Invalid authentication token", "INVALID_TOKEN"));
            }

            var profile = await _context.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null)
            {
                return NotFound(ApiResponse<object>.FailureResult(
                    "Profile not found", "PROFILE_NOT_FOUND"));
            }

            profile.FcmToken = null;
            profile.FcmPlatform = null;
            profile.FcmTokenUpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("FCM token removed for user {UserId}", userId);

            return Ok(ApiResponse<object>.SuccessResult(new { removed = true }));
        }
    }

    public class DeviceTokenRequest
    {
        public string Token { get; set; } = string.Empty;
        public string? Platform { get; set; }
    }
}
