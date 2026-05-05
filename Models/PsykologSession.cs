using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UserService.Models;

/// <summary>
/// Status of a PsykologSession.
/// Stored as int (EF default).
/// </summary>
public enum PsykologSessionStatus
{
    Active = 0,
    Completed = 1,
    Expired = 2
}

/// <summary>
/// Represents a psychologist/AI chat session for a user.
/// </summary>
[Table("PsykologSessions")]
public class PsykologSession
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Keycloak user ID that owns this session.
    /// </summary>
    [Required]
    public string KeycloakId { get; set; } = string.Empty;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? EndedAt { get; set; }

    public int ThemeCount { get; set; } = 0;

    public PsykologSessionStatus Status { get; set; } = PsykologSessionStatus.Active;

    /// <summary>
    /// Sequential session number per user (1-based).
    /// </summary>
    public int SessionNumber { get; set; }

    // Navigation property
    public ICollection<PsykologMessage> Messages { get; set; } = new List<PsykologMessage>();
}
