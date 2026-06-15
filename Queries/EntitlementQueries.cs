using MediatR;
using Microsoft.EntityFrameworkCore;
using UserService.Data;
using UserService.DTOs;
using UserService.Models;

namespace UserService.Queries;

public class GetEntitlementHandler : IRequestHandler<GetEntitlementQuery, GetEntitlementResponse>
{
    private readonly ApplicationDbContext _db;
    public GetEntitlementHandler(ApplicationDbContext db) => _db = db;

    public async Task<GetEntitlementResponse> Handle(GetEntitlementQuery request, CancellationToken ct)
    {
        var ent = await _db.Entitlements
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.UserId == request.UserId, ct);

        if (ent == null || (ent.Tier == EntitlementTier.Premium && ent.ExpiresAt < DateTime.UtcNow))
        {
            return new GetEntitlementResponse(request.UserId, EntitlementTier.Free, null, false);
        }

        return new GetEntitlementResponse(request.UserId, ent.Tier, ent.ExpiresAt, ent.Tier == EntitlementTier.Premium);
    }
}

public class GetSparksBalanceHandler : IRequestHandler<GetSparksBalanceQuery, GetSparksBalanceResponse>
{
    private readonly ApplicationDbContext _db;
    public GetSparksBalanceHandler(ApplicationDbContext db) => _db = db;

    public async Task<GetSparksBalanceResponse> Handle(GetSparksBalanceQuery request, CancellationToken ct)
    {
        var last = await _db.SparksLedger
            .AsNoTracking()
            .Where(s => s.UserId == request.UserId)
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync(ct);

        return new GetSparksBalanceResponse(request.UserId, last?.BalanceAfter ?? 0);
    }
}
