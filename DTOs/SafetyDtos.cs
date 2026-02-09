using System.ComponentModel.DataAnnotations;

namespace UserService.DTOs;

/// <summary>
/// Request to block another user.
/// </summary>
public class BlockUserDto
{
    [Required]
    public string TargetUserId { get; set; } = string.Empty;
}

/// <summary>
/// Request to submit a safety report.
/// </summary>
public class SafetyReportDto
{
    [Required]
    public string SubjectType { get; set; } = string.Empty; // "user", "message", "photo"

    [Required]
    public string SubjectId { get; set; } = string.Empty;

    [Required]
    public string Reason { get; set; } = string.Empty;

    public string? Description { get; set; }
}

/// <summary>
/// Response after blocking a user.
/// </summary>
public class BlockResponseDto
{
    public string BlockerId { get; set; } = string.Empty;
    public string BlockedUserId { get; set; } = string.Empty;
    public DateTime BlockedAt { get; set; }
}

/// <summary>
/// Response for a safety report.
/// </summary>
public class SafetyReportResponseDto
{
    public string ReportId { get; set; } = string.Empty;
    public string SubjectType { get; set; } = string.Empty;
    public string SubjectId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Response for block-check queries (used by other services).
/// </summary>
public class IsBlockedResponseDto
{
    public bool IsBlocked { get; set; }
}
