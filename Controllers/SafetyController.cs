using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using UserService.Common;
using UserService.DTOs;
using UserService.Services;

namespace UserService.Controllers;

/// <summary>
/// Safety and trust endpoints — blocking users, submitting reports, checking block status.
/// Implements the API spec: POST /safety/report, POST /safety/block, GET /safety/audit.
/// Also provides GET /safety/is-blocked/{targetUserId} used by messaging-service and photo-service.
/// </summary>
[Route("api/safety")]
[ApiController]
[Authorize]
public class SafetyController : ControllerBase
{
    private readonly ISafetyService _safetyService;
    private readonly ILogger<SafetyController> _logger;

    public SafetyController(ISafetyService safetyService, ILogger<SafetyController> logger)
    {
        _safetyService = safetyService;
        _logger = logger;
    }

    private string? GetAuthenticatedUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst("sub")?.Value;
    }

    /// <summary>
    /// Block another user. Blocked users cannot send messages or view detailed photos.
    /// </summary>
    [HttpPost("block")]
    public async Task<ActionResult<ApiResponse<BlockResponseDto>>> BlockUser([FromBody] BlockUserDto request)
    {
        var userId = GetAuthenticatedUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<BlockResponseDto>.FailureResult("Invalid token", "INVALID_TOKEN"));

        if (userId == request.TargetUserId)
            return BadRequest(ApiResponse<BlockResponseDto>.FailureResult("Cannot block yourself", "SELF_BLOCK"));

        await _safetyService.BlockUserAsync(userId, request.TargetUserId);

        var response = new BlockResponseDto
        {
            BlockerId = userId,
            BlockedUserId = request.TargetUserId,
            BlockedAt = DateTime.UtcNow
        };

        return Ok(ApiResponse<BlockResponseDto>.SuccessResult(response));
    }

    /// <summary>
    /// Unblock a previously blocked user.
    /// </summary>
    [HttpPost("unblock")]
    public async Task<ActionResult<ApiResponse<BlockResponseDto>>> UnblockUser([FromBody] BlockUserDto request)
    {
        var userId = GetAuthenticatedUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<BlockResponseDto>.FailureResult("Invalid token", "INVALID_TOKEN"));

        await _safetyService.UnblockUserAsync(userId, request.TargetUserId);

        var response = new BlockResponseDto
        {
            BlockerId = userId,
            BlockedUserId = request.TargetUserId,
            BlockedAt = DateTime.UtcNow
        };

        return Ok(ApiResponse<BlockResponseDto>.SuccessResult(response));
    }

    /// <summary>
    /// Check if a user is blocked (used internally by messaging-service and photo-service).
    /// </summary>
    [HttpGet("is-blocked/{targetUserId}")]
    public async Task<ActionResult<ApiResponse<IsBlockedResponseDto>>> IsBlocked(string targetUserId)
    {
        var userId = GetAuthenticatedUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<IsBlockedResponseDto>.FailureResult("Invalid token", "INVALID_TOKEN"));

        var isBlocked = await _safetyService.IsBlockedAsync(userId, targetUserId);
        return Ok(ApiResponse<IsBlockedResponseDto>.SuccessResult(new IsBlockedResponseDto { IsBlocked = isBlocked }));
    }

    /// <summary>
    /// Get list of blocked users for the authenticated user.
    /// </summary>
    [HttpGet("blocked")]
    public async Task<ActionResult<ApiResponse<List<BlockResponseDto>>>> GetBlockedUsers()
    {
        var userId = GetAuthenticatedUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<List<BlockResponseDto>>.FailureResult("Invalid token", "INVALID_TOKEN"));

        var blocks = await _safetyService.GetBlockedUsersAsync(userId);
        var result = blocks.Select(b => new BlockResponseDto
        {
            BlockerId = b.BlockerId,
            BlockedUserId = b.BlockedUserId,
            BlockedAt = b.CreatedAt
        }).ToList();

        return Ok(ApiResponse<List<BlockResponseDto>>.SuccessResult(result));
    }

    /// <summary>
    /// Submit a safety report about a user, message, or photo.
    /// </summary>
    [HttpPost("report")]
    public async Task<ActionResult<ApiResponse<SafetyReportResponseDto>>> SubmitReport([FromBody] SafetyReportDto request)
    {
        var userId = GetAuthenticatedUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<SafetyReportResponseDto>.FailureResult("Invalid token", "INVALID_TOKEN"));

        var validSubjectTypes = new[] { "user", "message", "photo" };
        if (!validSubjectTypes.Contains(request.SubjectType?.ToLower()))
        {
            return BadRequest(ApiResponse<SafetyReportResponseDto>.FailureResult(
                "Invalid subject type. Must be: user, message, or photo", "INVALID_SUBJECT_TYPE"));
        }

        var report = await _safetyService.SubmitReportAsync(
            userId, request.SubjectType, request.SubjectId, request.Reason, request.Description);

        var response = new SafetyReportResponseDto
        {
            ReportId = report.Id,
            SubjectType = report.SubjectType,
            SubjectId = report.SubjectId,
            Reason = report.Reason,
            Status = report.Status,
            CreatedAt = report.CreatedAt
        };

        return Ok(ApiResponse<SafetyReportResponseDto>.SuccessResult(response));
    }

    /// <summary>
    /// Admin: Get all reports (paginated). Future: add role-based auth.
    /// </summary>
    [HttpGet("audit")]
    public async Task<ActionResult<ApiResponse<List<SafetyReportResponseDto>>>> GetReports(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        var reports = await _safetyService.GetReportsAsync(page, pageSize);
        var result = reports.Select(r => new SafetyReportResponseDto
        {
            ReportId = r.Id,
            SubjectType = r.SubjectType,
            SubjectId = r.SubjectId,
            Reason = r.Reason,
            Status = r.Status,
            CreatedAt = r.CreatedAt
        }).ToList();

        return Ok(ApiResponse<List<SafetyReportResponseDto>>.SuccessResult(result));
    }
}
