using MediatR;
using Microsoft.EntityFrameworkCore;
using UserService.Data;
using UserService.DTOs;
using UserService.Models;

namespace UserService.Queries;

/// Returns Sparks balance + today's usage + allocation limits.
/// For Majestic users: daily allocation = 2. Free users: 0.
/// On first fetch each day, auto-credits the daily allocation.
public class GetSparksStatusHandler : IRequestHandler<GetSparksStatusQuery, GetSparksStatusResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly IMediator _mediator;

    public GetSparksStatusHandler(ApplicationDbContext db, IMediator mediator)
    {
        _db = db;
        _mediator = mediator;
    }

    public async Task<GetSparksStatusResponse> Handle(GetSparksStatusQuery request, CancellationToken ct)
    {
        // 1. Compute total balance
        var last = await _db.SparksLedger
            .AsNoTracking()
            .Where(s => s.UserId == request.UserId)
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync(ct);
        var balance = last?.BalanceAfter ?? 0;

        // 2. Count today's spark spends (actions that cost a spark)
        var todayStart = DateTime.UtcNow.Date;
        var dailyUsed = await _db.SparksLedger
            .AsNoTracking()
            .CountAsync(s => s.UserId == request.UserId
                          && s.Delta < 0
                          && s.CreatedAt >= todayStart, ct);

        // 3. Determine daily max based on tier
        var entitlement = await _mediator.Send(new GetEntitlementQuery(request.UserId), ct);
        var isPremium = entitlement.IsPremium;
        var dailyMax = isPremium ? 2 : 0;

        // 4. Auto-allocate daily Sparks for premium users if not yet done today
        if (isPremium && dailyUsed == 0 && dailyMax > 0)
        {
            // Check if we already granted today's allocation
            var grantedToday = await _db.SparksLedger
                .AsNoTracking()
                .AnyAsync(s => s.UserId == request.UserId
                            && s.Reason == "daily_allocation"
                            && s.CreatedAt >= todayStart, ct);

            if (!grantedToday)
            {
                await _mediator.Send(new CreditSparksCommand(request.UserId, dailyMax, "daily_allocation"), ct);
                balance += dailyMax;
            }
        }

        var remaining = Math.Max(0, dailyMax - dailyUsed);

        return new GetSparksStatusResponse(request.UserId, balance, dailyUsed, dailyMax, remaining);
    }
}

/// Spend a Spark for an action (ping, rewind, super-like).
/// Checks: user must have daily allocation remaining OR purchased Sparks in balance.
public class SpendSparkHandler : IRequestHandler<SpendSparkCommand, SpendSparkResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly IMediator _mediator;

    public SpendSparkHandler(ApplicationDbContext db, IMediator mediator)
    {
        _db = db;
        _mediator = mediator;
    }

    public async Task<SpendSparkResponse> Handle(SpendSparkCommand request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.Action))
            return new SpendSparkResponse(false, 0, 0, "Action is required");

        var validActions = new[] { "spark_ping", "rewind", "super_like", "boost" };
        if (!validActions.Contains(request.Action))
            return new SpendSparkResponse(false, 0, 0, $"Invalid action: {request.Action}");

        // Compute daily status inline (avoids MediatR recursion for testability)
        var todayStart = DateTime.UtcNow.Date;
        var dailyUsed = await _db.SparksLedger
            .CountAsync(s => s.UserId == request.UserId && s.Delta < 0 && s.CreatedAt >= todayStart, ct);

        var entitlement = await _mediator.Send(new GetEntitlementQuery(request.UserId), ct);
        var dailyMax = entitlement.IsPremium ? 2 : 0;
        var dailyRemaining = Math.Max(0, dailyMax - dailyUsed);

        var last = await _db.SparksLedger
            .Where(s => s.UserId == request.UserId)
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync(ct);
        var totalBalance = last?.BalanceAfter ?? 0;

        // Try to use daily allocation first (Feeld-like: 2 pings/day)
        if (dailyRemaining > 0)
        {
            var result = await _mediator.Send(new DebitSparksCommand(request.UserId, 1, request.Action), ct);
            if (result.Success)
                return new SpendSparkResponse(true, result.NewBalance, dailyRemaining - 1, null);
        }

        // Fallback: spend from purchased Sparks balance
        if (totalBalance > 0)
        {
            var result = await _mediator.Send(new DebitSparksCommand(request.UserId, 1, request.Action), ct);
            if (result.Success)
                return new SpendSparkResponse(true, result.NewBalance, dailyRemaining, null);
        }

        return new SpendSparkResponse(false, totalBalance, dailyRemaining, "No Sparks available. Purchase a bundle or upgrade to Majestic.");
    }
}
