using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using UserService.Common;
using UserService.Data;
using UserService.DTOs;

namespace UserService.Controllers;

/// <summary>
/// Handles profile verification status queries and verification requests.
/// Part of the trust & safety feature set for the MVP.
/// </summary>
[Route("api/verification")]
[ApiController]
[Authorize]
public class VerificationController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<VerificationController> _logger;

    public VerificationController(
        ApplicationDbContext context,
        ILogger<VerificationController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get verification status for a user.
    /// </summary>
    [HttpGet("{userId}")]
    public async Task<ActionResult<ApiResponse<VerificationStatusDto>>> GetVerificationStatus(string userId)
    {
        try
        {
            if (!Guid.TryParse(userId, out var userGuid))
            {
                return BadRequest(ApiResponse<VerificationStatusDto>.FailureResult(
                    "Invalid user ID format", "INVALID_USER_ID"));
            }

            var profile = await _context.UserProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userGuid);

            if (profile == null)
            {
                return NotFound(ApiResponse<VerificationStatusDto>.FailureResult(
                    "User profile not found", "PROFILE_NOT_FOUND"));
            }

            // For MVP, verification is based on profile completeness:
            // - Email: always true if user registered via Keycloak (has valid JWT)
            // - Photo: true if user has at least 1 photo uploaded
            // - Phone: false (not yet implemented)
            var hasPhotos = !string.IsNullOrEmpty(profile.PhotoUrls);
            var emailVerified = true; // Keycloak-registered users have verified emails

            var dto = new VerificationStatusDto
            {
                PhotoVerified = hasPhotos,
                EmailVerified = emailVerified,
                PhoneVerified = false, // Not yet implemented
                OverallLevel = VerificationStatusDto.ComputeLevel(hasPhotos, emailVerified, false),
                LastVerifiedAt = profile.UpdatedAt,
                VerificationNote = hasPhotos
                    ? "Profile has photo verification"
                    : "Upload a photo to increase your verification level"
            };

            return Ok(ApiResponse<VerificationStatusDto>.SuccessResult(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving verification status for user {UserId}", userId);
            return StatusCode(500, ApiResponse<VerificationStatusDto>.FailureResult(
                "Internal server error", "INTERNAL_ERROR"));
        }
    }

    /// <summary>
    /// Request a new verification for the authenticated user.
    /// </summary>
    [HttpPost("{userId}/request")]
    public async Task<ActionResult<ApiResponse<VerificationRequestResponseDto>>> RequestVerification(
        string userId, [FromBody] VerificationRequestDto request)
    {
        try
        {
            // Ensure the authenticated user can only request their own verification
            var authenticatedUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                     ?? User.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(authenticatedUserId) || authenticatedUserId != userId)
            {
                return Forbid();
            }

            var validTypes = new[] { "photo", "email", "phone" };
            if (!validTypes.Contains(request.VerificationType?.ToLower()))
            {
                return BadRequest(ApiResponse<VerificationRequestResponseDto>.FailureResult(
                    "Invalid verification type. Must be: photo, email, or phone",
                    "INVALID_VERIFICATION_TYPE"));
            }

            // For MVP, just acknowledge the request
            // Future: queue verification job, send email/SMS, trigger photo review
            var response = new VerificationRequestResponseDto
            {
                UserId = userId,
                VerificationType = request.VerificationType!.ToLower(),
                Status = "Pending",
                RequestedAt = DateTime.UtcNow,
                Instructions = request.VerificationType.ToLower() switch
                {
                    "photo" => "Please upload a clear selfie for photo verification",
                    "email" => "A verification email will be sent to your registered address",
                    "phone" => "Phone verification is not yet available",
                    _ => null
                }
            };

            _logger.LogInformation("Verification request submitted: {Type} for user {UserId}",
                request.VerificationType, userId);

            return Ok(ApiResponse<VerificationRequestResponseDto>.SuccessResult(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing verification request for user {UserId}", userId);
            return StatusCode(500, ApiResponse<VerificationRequestResponseDto>.FailureResult(
                "Internal server error", "INTERNAL_ERROR"));
        }
    }
}
