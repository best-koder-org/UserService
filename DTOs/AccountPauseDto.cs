namespace UserService.DTOs;

/// <summary>Account status enum for account lifecycle management (T090).</summary>
public enum AccountStatus
{
    Active,
    Paused,
    Deactivated,
    Deleted
}

/// <summary>Duration options for account pause/snooze.</summary>
public enum PauseDuration
{
    Hours24,
    Hours72,
    OneWeek,
    Indefinite
}

/// <summary>Request DTO for pausing an account.</summary>
public record AccountPauseRequest(
    PauseDuration Duration,
    string? Reason = null
);

/// <summary>Response DTO for account status queries.</summary>
public record AccountStatusResponse(
    string UserId,
    AccountStatus Status,
    DateTime? PausedAt,
    DateTime? ResumeAt,
    PauseDuration? PauseDuration,
    string? PauseReason
);
