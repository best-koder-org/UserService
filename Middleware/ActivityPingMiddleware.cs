using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace UserService.Middleware;

/// <summary>
/// Middleware that sends a fire-and-forget activity ping to MatchmakingService
/// on every authenticated API call, debounced to max 1 ping per user per 5 minutes.
/// T166: LastActiveAt sync integration.
/// </summary>
public class ActivityPingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ActivityPingMiddleware> _logger;
    private static readonly TimeSpan DebounceInterval = TimeSpan.FromMinutes(5);

    public ActivityPingMiddleware(
        RequestDelegate next,
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IConfiguration configuration,
        ILogger<ActivityPingMiddleware> logger)
    {
        _next = next;
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Continue the pipeline first — don't block the request
        await _next(context);

        // Only ping for authenticated requests
        if (context.User?.Identity?.IsAuthenticated != true)
            return;

        var userIdClaim = context.User.FindFirst("userId")
            ?? context.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            return;

        // Debounce: skip if we pinged this user in the last 5 minutes
        var cacheKey = $"activity-ping:{userId}";
        if (_cache.TryGetValue(cacheKey, out _))
            return;

        // Mark as pinged (even before sending — avoid flooding on failure)
        _cache.Set(cacheKey, true, DebounceInterval);

        // Fire-and-forget — don't await, don't block
        _ = Task.Run(async () =>
        {
            try
            {
                var baseUrl = _configuration["ServiceUrls:MatchmakingService"]
                    ?? "http://localhost:8083";
                var apiKey = _configuration["InternalAuth:MatchmakingApiKey"]
                    ?? "user-service-internal-key-dev-only";

                var client = _httpClientFactory.CreateClient();
                var request = new HttpRequestMessage(HttpMethod.Post,
                    $"{baseUrl}/api/internal/matchmaking/activity-ping");
                request.Headers.Add("X-Internal-API-Key", apiKey);
                request.Content = new StringContent(
                    JsonSerializer.Serialize(new { userId, lastActiveAt = DateTime.UtcNow }),
                    Encoding.UTF8,
                    "application/json");

                var response = await client.SendAsync(request, CancellationToken.None);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("Activity ping failed for user {UserId}: {StatusCode}",
                        userId, response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Activity ping error for user {UserId}", userId);
            }
        });
    }
}

public static class ActivityPingMiddlewareExtensions
{
    public static IApplicationBuilder UseActivityPing(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ActivityPingMiddleware>();
    }
}
