using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UserService.Models;

/// <summary>
/// Notification preferences for the authenticated user.
/// Controls which push notifications the user receives.
/// </summary>
[Table("NotificationPreferences")]
public class NotificationPreferences
{
    [Key]
    public int Id { get; set; }

    /// <summary>Foreign key to UserProfile</summary>
    public int UserProfileId { get; set; }

    /// <summary>Keycloak user ID for direct lookup</summary>
    public Guid UserId { get; set; }

    /// <summary>Master toggle for push notifications</summary>
    public bool PushEnabled { get; set; } = true;

    /// <summary>Notify on new matches</summary>
    public bool MatchNotifications { get; set; } = true;

    /// <summary>Notify on new messages</summary>
    public bool MessageNotifications { get; set; } = true;

    /// <summary>Notify when someone sends a Spark</summary>
    public bool SparkNotifications { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
