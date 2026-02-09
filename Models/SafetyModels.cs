namespace UserService.Models;

/// <summary>
/// Represents a user block relationship.
/// Stored in-memory for MVP; production: persistent table.
/// </summary>
public class UserBlock
{
    public string BlockerId { get; set; } = string.Empty;
    public string BlockedUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a user-submitted safety report.
/// </summary>
public class SafetyReport
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ReporterId { get; set; } = string.Empty;
    public string SubjectType { get; set; } = string.Empty; // "user", "message", "photo"
    public string SubjectId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "Open";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
