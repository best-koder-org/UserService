using System.ComponentModel.DataAnnotations;

namespace UserService.Models;

/// <summary>
/// T576 — Accumulated psykolog reflection vector for vector similarity matching.
/// One row per user, updated after each theme extraction.
/// Vectors stored as JSON float array string (MySQL-compatible).
/// </summary>
public class ReflectionVector
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(255)]
    public string KeycloakId { get; set; } = string.Empty;

    /// <summary>128-dim embedding stored as JSON float array string.</summary>
    [MaxLength(8192)]
    public string VectorJson { get; set; } = "[]";

    /// <summary>Number of psykolog sessions that contributed to this vector.</summary>
    public int SessionCount { get; set; }

    /// <summary>Confidence in the vector (0-1). Based on session count.</summary>
    public double Confidence { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
