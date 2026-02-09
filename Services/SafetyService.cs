using System.Collections.Concurrent;
using UserService.Models;

namespace UserService.Services;

/// <summary>
/// Interface for safety operations (blocking and reporting).
/// </summary>
public interface ISafetyService
{
    Task<bool> BlockUserAsync(string blockerId, string targetUserId);
    Task<bool> UnblockUserAsync(string blockerId, string targetUserId);
    Task<bool> IsBlockedAsync(string userId, string targetUserId);
    Task<List<UserBlock>> GetBlockedUsersAsync(string userId);
    Task<SafetyReport> SubmitReportAsync(string reporterId, string subjectType, string subjectId, string reason, string? description);
    Task<List<SafetyReport>> GetReportsAsync(int page = 1, int pageSize = 25);
}

/// <summary>
/// In-memory safety service for MVP. Production: replace with EF Core / persistent storage.
/// </summary>
public class SafetyService : ISafetyService
{
    private readonly ConcurrentDictionary<string, HashSet<string>> _blocks = new();
    private readonly ConcurrentBag<SafetyReport> _reports = new();
    private readonly ILogger<SafetyService> _logger;

    public SafetyService(ILogger<SafetyService> logger)
    {
        _logger = logger;
    }

    public Task<bool> BlockUserAsync(string blockerId, string targetUserId)
    {
        var blockedUsers = _blocks.GetOrAdd(blockerId, _ => new HashSet<string>());
        lock (blockedUsers)
        {
            blockedUsers.Add(targetUserId);
        }
        _logger.LogInformation("User {BlockerId} blocked {TargetUserId}", blockerId, targetUserId);
        return Task.FromResult(true);
    }

    public Task<bool> UnblockUserAsync(string blockerId, string targetUserId)
    {
        if (_blocks.TryGetValue(blockerId, out var blockedUsers))
        {
            lock (blockedUsers)
            {
                blockedUsers.Remove(targetUserId);
            }
            _logger.LogInformation("User {BlockerId} unblocked {TargetUserId}", blockerId, targetUserId);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<bool> IsBlockedAsync(string userId, string targetUserId)
    {
        // Check if either user has blocked the other (bidirectional)
        var blockedByUser = _blocks.TryGetValue(userId, out var userBlocks) &&
                            userBlocks.Contains(targetUserId);
        var blockedByTarget = _blocks.TryGetValue(targetUserId, out var targetBlocks) &&
                              targetBlocks.Contains(userId);
        return Task.FromResult(blockedByUser || blockedByTarget);
    }

    public Task<List<UserBlock>> GetBlockedUsersAsync(string userId)
    {
        if (_blocks.TryGetValue(userId, out var blockedUsers))
        {
            var result = blockedUsers.Select(b => new UserBlock
            {
                BlockerId = userId,
                BlockedUserId = b,
                CreatedAt = DateTime.UtcNow // MVP: we don't track individual block times in-memory
            }).ToList();
            return Task.FromResult(result);
        }
        return Task.FromResult(new List<UserBlock>());
    }

    public Task<SafetyReport> SubmitReportAsync(string reporterId, string subjectType, string subjectId, string reason, string? description)
    {
        var report = new SafetyReport
        {
            ReporterId = reporterId,
            SubjectType = subjectType,
            SubjectId = subjectId,
            Reason = reason,
            Description = description
        };
        _reports.Add(report);
        _logger.LogInformation("Safety report submitted: {ReportId} by {ReporterId} — {SubjectType}/{SubjectId}",
            report.Id, reporterId, subjectType, subjectId);
        return Task.FromResult(report);
    }

    public Task<List<SafetyReport>> GetReportsAsync(int page = 1, int pageSize = 25)
    {
        var reports = _reports
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
        return Task.FromResult(reports);
    }
}
