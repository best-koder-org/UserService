using System;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;
using UserService.Middleware;

namespace UserService.Tests.Middleware;

public class ActivityPingMiddlewareTests
{
    private bool _nextCalled;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<HttpMessageHandler> _httpHandlerMock;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;

    public ActivityPingMiddlewareTests()
    {
        _httpHandlerMock = new Mock<HttpMessageHandler>();
        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        var httpClient = new HttpClient(_httpHandlerMock.Object);
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        _cache = new MemoryCache(new MemoryCacheOptions());

        var configData = new System.Collections.Generic.Dictionary<string, string?>
        {
            { "ServiceUrls:MatchmakingService", "http://localhost:8083" },
            { "InternalAuth:MatchmakingApiKey", "test-key" }
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
    }

    private ActivityPingMiddleware CreateMiddleware()
    {
        _nextCalled = false;
        RequestDelegate next = _ =>
        {
            _nextCalled = true;
            return Task.CompletedTask;
        };
        return new ActivityPingMiddleware(next, _httpClientFactoryMock.Object,
            _cache, _configuration, Mock.Of<ILogger<ActivityPingMiddleware>>());
    }

    private DefaultHttpContext CreateAuthenticatedContext(int userId = 42)
    {
        var context = new DefaultHttpContext();
        var claims = new[] { new Claim("userId", userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        context.User = new ClaimsPrincipal(identity);
        return context;
    }

    private DefaultHttpContext CreateAnonymousContext()
    {
        return new DefaultHttpContext();
    }

    // ===== Pipeline Flow =====

    [Fact]
    public async Task AnyRequest_AlwaysCallsNext()
    {
        var middleware = CreateMiddleware();
        var context = CreateAuthenticatedContext();

        await middleware.InvokeAsync(context);

        Assert.True(_nextCalled);
    }

    [Fact]
    public async Task AnonymousRequest_CallsNextButNoPing()
    {
        var middleware = CreateMiddleware();
        var context = CreateAnonymousContext();

        await middleware.InvokeAsync(context);

        Assert.True(_nextCalled);
        // No HTTP call should be made for anonymous users
        _httpHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    // ===== Debounce =====

    [Fact]
    public async Task AuthenticatedRequest_FirstCall_SendsPing()
    {
        var middleware = CreateMiddleware();
        var context = CreateAuthenticatedContext(100);

        await middleware.InvokeAsync(context);

        // Wait briefly for fire-and-forget to execute
        await Task.Delay(100);

        _httpHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task AuthenticatedRequest_SecondCallWithinDebounce_DoesNotPingAgain()
    {
        var middleware = CreateMiddleware();
        var context1 = CreateAuthenticatedContext(200);
        var context2 = CreateAuthenticatedContext(200);

        await middleware.InvokeAsync(context1);
        await Task.Delay(50);
        await middleware.InvokeAsync(context2);
        await Task.Delay(100);

        // Only 1 ping should have been sent (debounced)
        _httpHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task DifferentUsers_BothGetPinged()
    {
        var middleware = CreateMiddleware();
        var ctx1 = CreateAuthenticatedContext(300);
        var ctx2 = CreateAuthenticatedContext(301);

        await middleware.InvokeAsync(ctx1);
        await middleware.InvokeAsync(ctx2);
        await Task.Delay(200);

        _httpHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Exactly(2),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    // ===== Error Resilience =====

    [Fact]
    public async Task PingFailure_DoesNotBlockRequest()
    {
        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var middleware = CreateMiddleware();
        var context = CreateAuthenticatedContext(400);

        await middleware.InvokeAsync(context);

        Assert.True(_nextCalled); // Pipeline should still complete
    }
}
