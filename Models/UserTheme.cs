namespace UserService.Models;

/// <summary>Extracted psychological theme for a user after a psykolog session.</summary>
public class UserTheme
{
    public int Id { get; set; }
    public string KeycloakId { get; set; } = string.Empty;
    public int SessionId { get; set; }

    /// <summary>e.g. "attachment_anxiety", "openness_to_experience"</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>0.0 – 1.0 intensity score</summary>
    public double Intensity { get; set; }

    /// <summary>BigFive | Attachment | Values</summary>
    public string Axis { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
