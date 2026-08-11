using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using UserService.Commands;
using UserService.Common;
using UserService.Data;
using UserService.DTOs;
using UserService.Queries;

namespace UserService.Controllers;

/// <summary>
/// Billing / monetization endpoints (P1 — sandbox stubs).
/// Real IAP receipt validation is a TODO; sandbox mode immediately grants items.
/// </summary>
[Route("api/billing")]
[ApiController]
[Authorize]
public class BillingController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<BillingController> _logger;
    private readonly IConfiguration _configuration;
    private readonly ApplicationDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;

    public BillingController(IMediator mediator, ILogger<BillingController> logger, IConfiguration configuration, ApplicationDbContext db, IHttpClientFactory httpClientFactory)
    {
        _mediator = mediator;
        _logger = logger;
        _configuration = configuration;
        _db = db;
        _httpClientFactory = httpClientFactory;
    }

    private string? GetUserId() =>
        User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? User?.FindFirstValue("sub");

    /// <summary>Get entitlement + sparks balance for the authenticated user.</summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<string>.FailureResult("Invalid token"));

        var entitlement = await _mediator.Send(new GetEntitlementQuery(userId));
        var sparks = await _mediator.Send(new GetSparksStatusQuery(userId));

        return Ok(new
        {
            entitlement.UserId,
            entitlement.Tier,
            entitlement.ExpiresAt,
            entitlement.IsPremium,
            SparksBalance = sparks.TotalBalance,
            SparksDailyUsed = sparks.DailyUsed,
            SparksDailyMax = sparks.DailyMax,
            SparksDailyRemaining = sparks.DailyRemaining,
        });
    }

    /// <summary>Spend a Spark for an action (ping, rewind, super-like).
    /// Feeld-inspired: Majestic users get 2 free pings/day.</summary>
    [HttpPost("sparks/spend")]
    public async Task<IActionResult> SpendSpark([FromBody] SpendSparkRequest request)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<string>.FailureResult("Invalid token"));

        if (string.IsNullOrWhiteSpace(request.Action))
            return BadRequest(ApiResponse<string>.FailureResult("Action is required"));

        var result = await _mediator.Send(new SpendSparkCommand(userId, request.Action.Trim().ToLower()));

        if (!result.Success)
            return StatusCode(402, ApiResponse<SpendSparkResponse>.FailureResult(result.Error ?? "Cannot spend Spark"));

        return Ok(ApiResponse<SpendSparkResponse>.SuccessResult(result));
    }

    /// <summary>Internal endpoint for service-to-service entitlement checks.
    /// Validated via X-Internal-API-Key header (same key other services use).</summary>
    [HttpGet("internal-status")]
    [AllowAnonymous]
    public async Task<IActionResult> GetInternalStatus([FromQuery] string userId)
    {
        // Validate internal API key
        var expectedKey = _configuration["InternalAuth:ApiKey"];
        var providedKey = Request.Headers["X-Internal-API-Key"].FirstOrDefault();
        if (string.IsNullOrEmpty(expectedKey) || providedKey != expectedKey)
            return Unauthorized(ApiResponse<string>.FailureResult("Invalid internal API key"));

        if (string.IsNullOrEmpty(userId))
            return BadRequest(ApiResponse<string>.FailureResult("userId query param required"));

        var entitlement = await _mediator.Send(new GetEntitlementQuery(userId));
        var balance = await _mediator.Send(new GetSparksBalanceQuery(userId));

        return Ok(new
        {
            entitlement.UserId,
            entitlement.Tier,
            entitlement.ExpiresAt,
            entitlement.IsPremium,
            SparksBalance = balance.Balance
        });
    }

    /// <summary>Premium catalog — hardcoded SKUs.</summary>
    [HttpGet("catalog")]
    [AllowAnonymous]
    public IActionResult GetCatalog()
    {
        var catalog = new PremiumCatalogResponse(
            Plans: new List<PremiumPlanSku>
            {
                new("premium_month", "Premium Månad", "Full tillgång i 30 dagar", 149, 30),
                new("premium_3months", "Premium Kvartal", "Full tillgång i 90 dagar — spara 20%", 299, 90),
                new("premium_year", "Premium År", "Full tillgång i 365 dagar — bästa värdet", 599, 365),
            },
            Bundles: new List<SparksBundleSku>
            {
                new("sparks_100", "Startpaket", 100, 199),
                new("sparks_500", "Boostpaket", 500, 699),
                new("sparks_1500", "Superspaket", 1500, 1499),
            }
        );
        return Ok(catalog);
    }

    /// <summary>Sandbox purchase — immediately grants the item. No real payment.</summary>
    [HttpPost("purchase")]
    public async Task<IActionResult> Purchase([FromBody] SandboxPurchaseRequest request)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<string>.FailureResult("Invalid token"));

        object? result = request.Sku switch
        {
            "premium_month" => await _mediator.Send(new GrantPremiumCommand(userId, 30)),
            "premium_3months" => await _mediator.Send(new GrantPremiumCommand(userId, 90)),
            "premium_year" => await _mediator.Send(new GrantPremiumCommand(userId, 365)),
            "sparks_100" => await _mediator.Send(new CreditSparksCommand(userId, 100, "purchase")),
            "sparks_500" => await _mediator.Send(new CreditSparksCommand(userId, 500, "purchase")),
            "sparks_1500" => await _mediator.Send(new CreditSparksCommand(userId, 1500, "purchase")),
            _ => null
        };

        if (result == null)
            return BadRequest(ApiResponse<string>.FailureResult("Unknown SKU"));

        _logger.LogInformation("Sandbox purchase: User={UserId} Sku={Sku}", userId, request.Sku);
        return Ok(ApiResponse<object>.SuccessResult(result, "Sandbox purchase complete"));
    }

    // ── Spark send/receive endpoints ──

    /// <summary>Send a Spark to another user. Deducts a Spark, creates a record, returns updated balance.</summary>
    [HttpPost("sparks/send")]
    public async Task<IActionResult> SendSpark([FromBody] SendSparkRequest request)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<string>.FailureResult("Invalid token"));

        if (string.IsNullOrWhiteSpace(request.RecipientUserId))
            return BadRequest(ApiResponse<string>.FailureResult("RecipientUserId is required"));

        if (request.RecipientUserId == userId)
            return BadRequest(ApiResponse<string>.FailureResult("Cannot send a Spark to yourself"));

        // Verify recipient exists as a real user profile
        if (!Guid.TryParse(request.RecipientUserId, out var recipientGuid))
            return BadRequest(ApiResponse<string>.FailureResult("Invalid recipient user ID"));
        var recipientExists = await _db.UserProfiles.AnyAsync(p => p.UserId == recipientGuid);
        if (!recipientExists)
            return BadRequest(ApiResponse<string>.FailureResult("Recipient user not found"));

        var result = await _mediator.Send(new SendSparkCommand(userId, request.RecipientUserId, request.Message));

        if (!result.Success)
            return StatusCode(402, ApiResponse<SendSparkResponse>.FailureResult(result.Error ?? "Cannot send Spark"));

        // Notify recipient in real-time via MatchmakingService SignalR (best-effort)
        _ = NotifyRecipientOfSparkAsync(userId, request.RecipientUserId, request.Message);

        return Ok(ApiResponse<SendSparkResponse>.SuccessResult(result, "Spark sent!"));
    }

    /// <summary>Get Sparks received by the authenticated user.</summary>
    [HttpGet("sparks/received")]
    public async Task<IActionResult> GetReceivedSparks([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<string>.FailureResult("Invalid token"));

        var result = await _mediator.Send(new GetReceivedSparksQuery(userId, page, pageSize));
        return Ok(ApiResponse<GetReceivedSparksResponse>.SuccessResult(result));
    }

    /// <summary>Get Sparks sent by the authenticated user.</summary>
    [HttpGet("sparks/sent")]
    public async Task<IActionResult> GetSentSparks([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<string>.FailureResult("Invalid token"));

        var result = await _mediator.Send(new GetSentSparksQuery(userId, page, pageSize));
        return Ok(ApiResponse<GetSentSparksResponse>.SuccessResult(result));
    }


    /// <summary>Mark a received Spark as read.</summary>
    [HttpPost("sparks/{sparkId}/read")]
    public async Task<IActionResult> MarkSparkRead(long sparkId)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<string>.FailureResult("Invalid token"));

        var spark = await _db.Sparks.FindAsync(sparkId);
        if (spark == null)
            return NotFound(ApiResponse<string>.FailureResult("Spark not found"));
        if (spark.RecipientUserId != userId)
            return Forbid();

        spark.IsRead = true;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<string>.SuccessResult("Marked as read"));
    }


    // ── Admin dashboard endpoints ──

    /// <summary>Notify recipient via SignalR that they received a Spark. Best-effort.</summary>
    private async Task NotifyRecipientOfSparkAsync(string senderUserId, string recipientUserId, string? message)
    {
        try
        {
            var matchmakingUrl = _configuration["Services:MatchmakingService"] ?? "http://matchmaking-service:8083";
            var internalKey = _configuration["InternalAuth:ApiKey"] ?? "user-service-internal-key-dev-only";

            var client = _httpClientFactory.CreateClient();
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                RecipientUserId = recipientUserId,
                SenderUserId = senderUserId,
                Message = message ?? ""
            });

            var httpContent = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
            httpContent.Headers.Add("X-Internal-API-Key", internalKey);

            var response = await client.PostAsync(
                $"{matchmakingUrl}/api/spark-notifications/notify",
                httpContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Spark notification to MatchmakingService returned {Status}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send spark notification (best-effort)");
        }
    }

    /// <summary>Admin billing overview — purchases, subscriptions, Sparks balances.
    /// Protected by internal API key (same as service-to-service).</summary>
    [HttpGet("admin/stats")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAdminStats()
    {
        var expectedKey = _configuration["InternalAuth:ApiKey"]
            ?? "user-service-internal-key-dev-only";
        var providedKey = Request.Headers["X-Internal-API-Key"].FirstOrDefault();
        if (string.IsNullOrEmpty(expectedKey) || providedKey != expectedKey)
            return Unauthorized(ApiResponse<string>.FailureResult("Invalid internal API key"));

        var stats = await _mediator.Send(new GetAdminBillingStatsQuery());
        return Ok(stats);
    }
}
