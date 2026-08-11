namespace UserService.DTOs;

public record GetNotificationPreferencesResponse
{
    public bool PushEnabled { get; init; } = true;
    public bool MatchNotifications { get; init; } = true;
    public bool MessageNotifications { get; init; } = true;
    public bool SparkNotifications { get; init; } = true;
    public DateTime UpdatedAt { get; init; }
}

public record UpdateNotificationPreferencesRequest
{
    public bool? PushEnabled { get; init; }
    public bool? MatchNotifications { get; init; }
    public bool? MessageNotifications { get; init; }
    public bool? SparkNotifications { get; init; }
}
