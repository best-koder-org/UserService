using MediatR;
using UserService.DTOs;
using UserService.Queries;

namespace UserService.Services;

/// <summary>
/// Reusable gate for checking premium entitlement from any service.
/// Used by MatchmakingService, swipe-service, etc. via HTTP calls to /api/billing/status.
/// Also used directly by controllers in this service via MediatR.
/// </summary>
public class FeatureGate : IFeatureGate
{
    private readonly IMediator _mediator;

    public FeatureGate(IMediator mediator) => _mediator = mediator;

    public async Task<bool> IsPremium(string userId)
    {
        var ent = await _mediator.Send(new GetEntitlementQuery(userId));
        return ent.IsPremium;
    }
}

public interface IFeatureGate
{
    Task<bool> IsPremium(string userId);
}
