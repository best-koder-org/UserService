using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UserService.Models;

/// <summary>
/// Role of the message author in a PsykologSession.
/// Stored as int (EF default).
/// </summary>
public enum PsykologRole
{
    User = 0,
    Assistant = 1
}

/// <summary>
/// Represents a single message within a PsykologSession.
/// </summary>
[Table("PsykologMessages")]
public class PsykologMessage
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to the owning PsykologSession (cascade delete).
    /// </summary>
    public int SessionId { get; set; }

    public PsykologRole Role { get; set; }

    [Required]
    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    [ForeignKey("SessionId")]
    public PsykologSession? Session { get; set; }
}
