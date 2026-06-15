using MediatR;
using UserService.Models;

namespace UserService.DTOs;

// ── Queries ──

public record GetEntitlementQuery(string UserId) : IRequest<GetEntitlementResponse>;
public record GetEntitlementResponse(string UserId, EntitlementTier Tier, DateTime? ExpiresAt, bool IsPremium);

public record GetSparksBalanceQuery(string UserId) : IRequest<GetSparksBalanceResponse>;
public record GetSparksBalanceResponse(string UserId, int Balance);

// ── Commands ──

public record GrantPremiumCommand(string UserId, int DurationDays) : IRequest<GrantPremiumResponse>;
public record GrantPremiumResponse(string UserId, EntitlementTier Tier, DateTime? ExpiresAt);

public record CreditSparksCommand(string UserId, int Amount, string Reason) : IRequest<CreditSparksResponse>;
public record CreditSparksResponse(string UserId, int NewBalance);

public record DebitSparksCommand(string UserId, int Amount, string Reason) : IRequest<DebitSparksResponse>;
public record DebitSparksResponse(string UserId, int NewBalance, bool Success, string? Error);

/// Sandbox purchase — immediately grants the item. No real payment processing.
public record SandboxPurchaseRequest(string Sku); // "premium_month", "sparks_500", etc.
public record SandboxPurchaseResponse(string Message, string? NewTier, int? SparksAwarded);

/// Spend a Spark for an action.
public record SpendSparkRequest(string Action);

// ── Catalog ──

public record PremiumPlanSku(string Sku, string Name, string Description, int PriceSparks, int DurationDays);
public record SparksBundleSku(string Sku, string Name, int Sparks, int PriceUsdCents);

public record PremiumCatalogResponse(List<PremiumPlanSku> Plans, List<SparksBundleSku> Bundles);
