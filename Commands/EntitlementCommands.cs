using System.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using UserService.Data;
using UserService.DTOs;
using UserService.Models;

namespace UserService.Commands;

public class GrantPremiumHandler : IRequestHandler<GrantPremiumCommand, GrantPremiumResponse>
{
    private readonly ApplicationDbContext _db;
    public GrantPremiumHandler(ApplicationDbContext db) => _db = db;

    public async Task<GrantPremiumResponse> Handle(GrantPremiumCommand request, CancellationToken ct)
    {
        var ent = await _db.Entitlements.FirstOrDefaultAsync(e => e.UserId == request.UserId, ct);
        if (ent == null)
        {
            ent = new Entitlement
            {
                UserId = request.UserId,
                Tier = EntitlementTier.Premium,
                ExpiresAt = DateTime.UtcNow.AddDays(request.DurationDays),
                CreatedAt = DateTime.UtcNow,
            };
            _db.Entitlements.Add(ent);
        }
        else
        {
            ent.Tier = EntitlementTier.Premium;
            // Extend from now (or extend existing expiry if already premium)
            var baseDate = ent.ExpiresAt.HasValue && ent.ExpiresAt > DateTime.UtcNow
                ? ent.ExpiresAt.Value
                : DateTime.UtcNow;
            ent.ExpiresAt = baseDate.AddDays(request.DurationDays);
            ent.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return new GrantPremiumResponse(request.UserId, EntitlementTier.Premium, ent.ExpiresAt);
    }
}

public class CreditSparksHandler : IRequestHandler<CreditSparksCommand, CreditSparksResponse>
{
    private readonly ApplicationDbContext _db;
    public CreditSparksHandler(ApplicationDbContext db) => _db = db;

    public async Task<CreditSparksResponse> Handle(CreditSparksCommand request, CancellationToken ct)
    {
        var last = await _db.SparksLedger
            .Where(s => s.UserId == request.UserId)
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync(ct);

        var currentBalance = last?.BalanceAfter ?? 0;
        var entry = new SparksLedgerEntry
        {
            UserId = request.UserId,
            Delta = request.Amount,
            Reason = request.Reason,
            BalanceAfter = currentBalance + request.Amount,
            CreatedAt = DateTime.UtcNow,
        };
        _db.SparksLedger.Add(entry);
        await _db.SaveChangesAsync(ct);
        return new CreditSparksResponse(request.UserId, entry.BalanceAfter);
    }
}

public class DebitSparksHandler : IRequestHandler<DebitSparksCommand, DebitSparksResponse>
{
    private readonly ApplicationDbContext _db;
    public DebitSparksHandler(ApplicationDbContext db) => _db = db;

    public async Task<DebitSparksResponse> Handle(DebitSparksCommand request, CancellationToken ct)
    {
        if (request.Amount <= 0)
            return new DebitSparksResponse(request.UserId, 0, false, "Amount must be positive");

        // Use serializable transaction to prevent double-spend race condition.
        // Falls back to no transaction for InMemoryDatabase (tests).
        var useTransaction = _db.Database.IsRelational();
        if (useTransaction)
            await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var last = await _db.SparksLedger
                .Where(s => s.UserId == request.UserId)
                .OrderByDescending(s => s.Id)
                .FirstOrDefaultAsync(ct);

            var currentBalance = last?.BalanceAfter ?? 0;
            if (currentBalance < request.Amount)
            {
                if (useTransaction) await _db.Database.RollbackTransactionAsync(ct);
                return new DebitSparksResponse(request.UserId, currentBalance, false, "Insufficient Sparks");
            }

            var entry = new SparksLedgerEntry
            {
                UserId = request.UserId,
                Delta = -request.Amount,
                Reason = request.Reason,
                BalanceAfter = currentBalance - request.Amount,
                CreatedAt = DateTime.UtcNow,
            };
            _db.SparksLedger.Add(entry);
            await _db.SaveChangesAsync(ct);
            if (useTransaction) await _db.Database.CommitTransactionAsync(ct);
            return new DebitSparksResponse(request.UserId, entry.BalanceAfter, true, null);
        }
        catch
        {
            if (useTransaction) await _db.Database.RollbackTransactionAsync(ct);
            throw;
        }
    }
}
