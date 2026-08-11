using MediatR;
using UserService.Models;

namespace UserService.DTOs;

// ── Sparks daily status (Feeld-inspired: Majestic gets 2/day) ──

/// Daily Sparks status — how many the user has used today vs their allocation.
public record GetSparksStatusQuery(string UserId) : IRequest<GetSparksStatusResponse>;
public record GetSparksStatusResponse(string UserId, int TotalBalance, int DailyUsed, int DailyMax, int DailyRemaining);

/// Spend a Spark (ping, rewind, super-like, etc.). Enforces daily allocation + balance.
public record SpendSparkCommand(string UserId, string Action) : IRequest<SpendSparkResponse>;
public record SpendSparkResponse(bool Success, int NewBalance, int DailyRemaining, string? Error);

// ── Spark send/receive (Feeld-style ping) ──

/// Request to send a Spark to another user.
public record SendSparkRequest(string RecipientUserId, string? Message);

/// Command to send a Spark (deducts Spark + creates record + notifies recipient).
public record SendSparkCommand(string SenderUserId, string RecipientUserId, string? Message) : IRequest<SendSparkResponse>;
public record SendSparkResponse(bool Success, int NewBalance, int DailyRemaining, string? Error, long? SparkRecordId);

/// Query sparks received by a user.
public record GetReceivedSparksQuery(string UserId, int Page = 1, int PageSize = 50) : IRequest<GetReceivedSparksResponse>;
public record GetReceivedSparksResponse(List<SparkRecordDto> Sparks, int TotalCount);

/// Query sparks sent by a user.
public record GetSentSparksQuery(string UserId, int Page = 1, int PageSize = 50) : IRequest<GetSentSparksResponse>;
public record GetSentSparksResponse(List<SparkRecordDto> Sparks, int TotalCount);

/// Lightweight DTO for spark records returned to clients.
public record SparkRecordDto(
    long Id,
    string SenderUserId,
    string RecipientUserId,
    string? Message,
    bool IsRead,
    DateTime CreatedAt,
    string? SenderDisplayName,
    string? SenderPhotoUrl,
    string? RecipientDisplayName,
    string? RecipientPhotoUrl
);
