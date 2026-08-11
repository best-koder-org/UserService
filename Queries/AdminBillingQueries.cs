using MediatR;
using Microsoft.EntityFrameworkCore;
using UserService.Common;
using UserService.Data;
using UserService.DTOs;

namespace UserService.Queries;

public record GetAdminBillingStatsQuery : IRequest<AdminBillingStatsResponse>;

public record AdminBillingStatsResponse(
    List<PremiumPurchaseSummary> RecentPurchases,
    int TotalPremiumUsers,
    int TotalPurchases,
    int TotalSparksCredited,
    int TotalSparksSpent,
    List<UserSparksSummary> TopSparksUsers,
    List<SubscriptionSummary> ActiveSubscriptions,
    DateTime GeneratedAt
);

public record PremiumPurchaseSummary(string UserId, string Sku, DateTime PurchasedAt);
public record UserSparksSummary(string UserId, int Balance, int DailyUsed);
public record SubscriptionSummary(string UserId, string Tier, DateTime? ExpiresAt, int DaysRemaining);

public class GetAdminBillingStatsHandler : IRequestHandler<GetAdminBillingStatsQuery, AdminBillingStatsResponse>
{
    private readonly ApplicationDbContext _db;
    public GetAdminBillingStatsHandler(ApplicationDbContext db) => _db = db;

    public async Task<AdminBillingStatsResponse> Handle(GetAdminBillingStatsQuery request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var thirtyDaysAgo = now.AddDays(-30);
        var todayStart = now.Date;

        // Recent purchases (last 30 days) — simple queries, materialized
        var recentRaw = await _db.SparksLedger
            .Where(s => s.Reason == "purchase" && s.CreatedAt >= thirtyDaysAgo)
            .OrderByDescending(s => s.CreatedAt)
            .Take(50)
            .Select(s => new { s.UserId, s.Delta, s.CreatedAt })
            .ToListAsync(ct);

        var entitlements = await _db.Entitlements
            .Where(e => e.Tier == Models.EntitlementTier.Premium && e.UpdatedAt >= thirtyDaysAgo)
            .OrderByDescending(e => e.UpdatedAt)
            .Take(50)
            .Select(e => new { e.UserId, e.UpdatedAt })
            .ToListAsync(ct);

        // Combine in-memory
        var purchases = recentRaw
            .Select(p => new PremiumPurchaseSummary(p.UserId, $"sparks_{Math.Abs(p.Delta)}", p.CreatedAt))
            .Concat(entitlements.Select(e => new PremiumPurchaseSummary(e.UserId, "premium", e.UpdatedAt)))
            .OrderByDescending(p => p.PurchasedAt)
            .Take(50)
            .ToList();

        // Count stats
        var totalPremiumUsers = await _db.Entitlements
            .CountAsync(e => e.Tier == Models.EntitlementTier.Premium
                          && (e.ExpiresAt == null || e.ExpiresAt > now), ct);

        var totalPurchases = await _db.SparksLedger.CountAsync(s => s.Reason == "purchase", ct);

        var totalCredited = await _db.SparksLedger
            .Where(s => s.Delta > 0)
            .SumAsync(s => (long)s.Delta, ct);
        var totalSpent = await _db.SparksLedger
            .Where(s => s.Delta < 0)
            .SumAsync(s => (long)Math.Abs(s.Delta), ct);

        // Top Sparks users — materialize all ledger entries
        var allEntries = await _db.SparksLedger
            .Where(s => s.Delta != 0)
            .Select(s => new { s.UserId, s.BalanceAfter, s.Delta, s.CreatedAt })
            .ToListAsync(ct);

        var topUsers = allEntries
            .GroupBy(s => s.UserId)
            .Select(g =>
            {
                var ordered = g.OrderByDescending(x => x.CreatedAt).ToList();
                return new UserSparksSummary(
                    g.Key,
                    ordered.First().BalanceAfter,
                    g.Count(x => x.Delta < 0 && x.CreatedAt >= todayStart)
                );
            })
            .OrderByDescending(u => u.Balance)
            .Take(20)
            .ToList();

        // Active subscriptions
        var activeSubs = await _db.Entitlements
            .Where(e => e.Tier == Models.EntitlementTier.Premium)
            .OrderByDescending(e => e.ExpiresAt)
            .Take(50)
            .Select(e => new { e.UserId, e.Tier, e.ExpiresAt })
            .ToListAsync(ct);

        var subResults = activeSubs.Select(s => new SubscriptionSummary(
            s.UserId, s.Tier.ToString(), s.ExpiresAt,
            s.ExpiresAt.HasValue ? Math.Max(0, (int)(s.ExpiresAt.Value - now).TotalDays) : 0
        )).ToList();

        return new AdminBillingStatsResponse(
            purchases, totalPremiumUsers, totalPurchases,
            (int)totalCredited, (int)totalSpent,
            topUsers, subResults, now);
    }
}
