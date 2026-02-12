namespace UserService.DTOs;

/// <summary>Categories for support tickets (T091).</summary>
public enum SupportTicketCategory
{
    Bug,
    Feature,
    Account,
    Safety,
    Other
}

/// <summary>Status tracking for support tickets.</summary>
public enum TicketStatus
{
    Open,
    InProgress,
    Resolved,
    Closed
}

/// <summary>Request DTO for creating a support ticket / feedback.</summary>
public record CreateSupportTicketRequest(
    SupportTicketCategory Category,
    string Subject,
    string Description,
    string? ContactEmail = null
);

/// <summary>Response DTO for support ticket details.</summary>
public record SupportTicketResponse(
    string TicketId,
    string UserId,
    SupportTicketCategory Category,
    TicketStatus Status,
    string Subject,
    string Description,
    string? ContactEmail,
    DateTime CreatedAt,
    DateTime? ResolvedAt
);
