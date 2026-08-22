using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using UserService.Data;
using UserService.Models;
using UserService.Services;

namespace UserService.Tests.Services;

public class VectorEmbeddingServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly VectorEmbeddingService _service;

    public VectorEmbeddingServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"VectorTests_{Guid.NewGuid()}")
            .Options;
        _context = new ApplicationDbContext(options);

        _service = new VectorEmbeddingService(
            _context,
            new Mock<ILogger<VectorEmbeddingService>>().Object,
            new Mock<IHttpClientFactory>().Object,
            new Mock<IConfiguration>().Object);
    }

    // ── BuildVectorFromThemes ─────────────────────────────────────────────

    [Fact]
    public void BuildVector_NoThemes_ReturnsZeroVector()
    {
        var v = VectorEmbeddingService.BuildVectorFromThemes(new List<UserTheme>());
        Assert.Equal(128, v.Length);
        Assert.All(v, x => Assert.Equal(0f, x));
    }

    [Fact]
    public void BuildVector_WithThemes_ProducesNonZeroNormalisedVector()
    {
        var themes = new List<UserTheme>
        {
            new() { KeycloakId = "u1", Label = "Openness", Intensity = 0.8, Axis = "BigFive", SessionId = 1, CreatedAt = DateTime.UtcNow },
            new() { KeycloakId = "u1", Label = "Anxious", Intensity = 0.6, Axis = "Attachment", SessionId = 1, CreatedAt = DateTime.UtcNow }
        };
        var v = VectorEmbeddingService.BuildVectorFromThemes(themes);
        Assert.Equal(128, v.Length);
        var norm = Math.Sqrt(v.Sum(x => x * x));
        Assert.True(Math.Abs(norm - 1.0) < 0.001, "Vector should be L2-normalised");
    }

    [Fact]
    public void BuildVector_SameLabelSameSlot_Idempotent()
    {
        var t = new UserTheme { KeycloakId = "u", Label = "Warmth", Intensity = 0.5, Axis = "BigFive", SessionId = 1, CreatedAt = DateTime.UtcNow };
        var v1 = VectorEmbeddingService.BuildVectorFromThemes(new[] { t });
        var v2 = VectorEmbeddingService.BuildVectorFromThemes(new[] { t });
        Assert.Equal(v1, v2);
    }

    // ── CalculateConfidence ───────────────────────────────────────────────

    [Theory]
    [InlineData(0, 0.0)]
    [InlineData(1, 0.4)]
    [InlineData(3, 0.7)]
    [InlineData(10, 0.95)]
    [InlineData(20, 0.95)]
    public void CalculateConfidence_KnownInputs(int sessions, double expected)
    {
        var c = VectorEmbeddingService.CalculateConfidence(sessions);
        Assert.InRange(c, expected - 0.01, expected + 0.01);
    }

    // ── UpdateVectorAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task UpdateVector_InsertsNewRow()
    {
        _context.UserThemes.Add(new UserTheme
        {
            KeycloakId = "user1", Label = "Adventurous", Intensity = 0.7,
            Axis = "BigFive", SessionId = 1, CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        await _service.UpdateVectorAsync("user1");

        var row = await _context.ReflectionVectors
            .FirstOrDefaultAsync(r => r.KeycloakId == "user1");
        Assert.NotNull(row);
        Assert.Equal(1, row.SessionCount);
        Assert.True(row.Confidence > 0);

        var v = JsonSerializer.Deserialize<float[]>(row.VectorJson);
        Assert.NotNull(v);
        Assert.Equal(128, v!.Length);
    }

    [Fact]
    public async Task UpdateVector_UpdatesExistingRow()
    {
        _context.ReflectionVectors.Add(new ReflectionVector
        {
            KeycloakId = "user2", VectorJson = "[]", SessionCount = 1, Confidence = 0.4, UpdatedAt = DateTime.UtcNow.AddDays(-1)
        });
        await _context.SaveChangesAsync();

        _context.UserThemes.Add(new UserTheme
        {
            KeycloakId = "user2", Label = "Kind", Intensity = 0.9,
            Axis = "Values", SessionId = 5, CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        await _service.UpdateVectorAsync("user2");

        var rows = _context.ReflectionVectors.Where(r => r.KeycloakId == "user2").ToList();
        Assert.Single(rows); // should update, not insert
    }

    // ── CosineSimilarityAsync ─────────────────────────────────────────────

    [Fact]
    public async Task CosineSimilarity_BothMissing_ReturnsNull()
    {
        var result = await _service.CosineSimilarityAsync("x", "y");
        Assert.Null(result);
    }

    [Fact]
    public async Task CosineSimilarity_IdenticalVectors_ReturnsOne()
    {
        var vec = JsonSerializer.Serialize(Enumerable.Repeat(1f / (float)Math.Sqrt(128), 128).ToArray());
        _context.ReflectionVectors.AddRange(
            new ReflectionVector { KeycloakId = "a", VectorJson = vec, SessionCount = 1, Confidence = 0.4 },
            new ReflectionVector { KeycloakId = "b", VectorJson = vec, SessionCount = 1, Confidence = 0.4 }
        );
        await _context.SaveChangesAsync();

        var result = await _service.CosineSimilarityAsync("a", "b");
        Assert.NotNull(result);
        Assert.True(result!.Value > 0.99);
    }

    // ── Real embedding path (P1) ─────────────────────────────────────────

    [Fact]
    public async Task UpdateVector_EmbeddingsEnabledButNoKey_SavesNothing()
    {
        var config = ConfigWithEmbeddings(enabled: true, apiKey: null);
        _context.UserThemes.Add(new UserTheme
        {
            KeycloakId = "u1", Label = "Openness", Axis = "BigFive", Intensity = 0.8,
            SessionId = 1, CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var svc = BuildService(_context, config, new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") }));

        var result = await svc.UpdateVectorAsync("u1");

        Assert.Empty(result);
        Assert.Empty(_context.ReflectionVectors.Where(r => r.KeycloakId == "u1"));
    }

    [Fact]
    public async Task UpdateVector_EmbeddingsEnabledAndSuccess_PersistsProviderVector()
    {
        var config = ConfigWithEmbeddings(enabled: true);
        _context.UserThemes.Add(new UserTheme
        {
            KeycloakId = "u2", Label = "Openness", Axis = "BigFive", Intensity = 0.8,
            SessionId = 1, CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var svc = BuildService(_context, config, new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"data\":[{\"embedding\":[0.6,0.0,0.8]}]}")
            }));

        var result = await svc.UpdateVectorAsync("u2");

        // Provider dimension preserved (no forced 128)
        Assert.Equal(3, result.Length);

        var row = _context.ReflectionVectors.Single(r => r.KeycloakId == "u2");
        var stored = JsonSerializer.Deserialize<float[]>(row.VectorJson);
        Assert.Equal(3, stored!.Length);

        // L2-normalised: sqrt(0.6^2 + 0.8^2) == 1
        var mag = Math.Sqrt(stored.Sum(v => (double)v * v));
        Assert.True(Math.Abs(mag - 1.0) < 0.001, $"expected unit length, got {mag}");
    }

    [Fact]
    public async Task UpdateVector_EmbeddingsEnabledButApiFails_SavesNothing()
    {
        var config = ConfigWithEmbeddings(enabled: true);
        _context.UserThemes.Add(new UserTheme
        {
            KeycloakId = "u3", Label = "Openness", Axis = "BigFive", Intensity = 0.8,
            SessionId = 1, CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var svc = BuildService(_context, config,
            new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        var result = await svc.UpdateVectorAsync("u3");

        Assert.Empty(result);
        Assert.Empty(_context.ReflectionVectors.Where(r => r.KeycloakId == "u3"));
    }

    [Fact]
    public async Task UpdateVector_EmbeddingsLocalKeyless_SucceedsAndSendsNoAuth()
    {
        var config = ConfigWithEmbeddings(enabled: true, requireApiKey: false, apiKey: null);
        _context.UserThemes.Add(new UserTheme
        {
            KeycloakId = "u4", Label = "Warmth", Axis = "Values", Intensity = 0.7,
            SessionId = 1, CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        HttpRequestMessage? captured = null;
        var svc = BuildService(_context, config, new StubHandler(req =>
        {
            captured = req;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"data\":[{\"embedding\":[0.5,0.5]}]}")
            };
        }));

        var result = await svc.UpdateVectorAsync("u4");

        Assert.Equal(2, result.Length);
        Assert.NotNull(captured);
        Assert.Null(captured!.Headers.Authorization); // keyless local: no auth header
        Assert.Single(_context.ReflectionVectors.Where(r => r.KeycloakId == "u4"));
    }

    [Fact]
    public async Task UpdateVector_EmbeddingsCloudRequiresKey_StillSkips()
    {
        // Default RequireApiKey=true (cloud fail-safe): missing key -> skip, save nothing.
        var config = ConfigWithEmbeddings(enabled: true, requireApiKey: true, apiKey: null);
        _context.UserThemes.Add(new UserTheme
        {
            KeycloakId = "u5", Label = "Warmth", Axis = "Values", Intensity = 0.7,
            SessionId = 1, CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var svc = BuildService(_context, config, new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") }));

        var result = await svc.UpdateVectorAsync("u5");

        Assert.Empty(result);
        Assert.Empty(_context.ReflectionVectors.Where(r => r.KeycloakId == "u5"));
    }

    public void Dispose() => _context.Dispose();

    // ── Helpers (real embedding path) ─────────────────────────────────────

    private static IConfiguration ConfigWithEmbeddings(bool enabled, bool requireApiKey = true, string? apiKey = "test-key") =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Embeddings:Enabled"] = enabled ? "true" : "false",
            ["Embeddings:RequireApiKey"] = requireApiKey ? "true" : "false",
            ["Embeddings:ApiKey"] = apiKey ?? string.Empty,
            ["Embeddings:BaseUrl"] = "https://embeddings.test/v1",
            ["Embeddings:Model"] = "test-embed",
            ["Embeddings:TimeoutSeconds"] = "5",
        }).Build();

    private static VectorEmbeddingService BuildService(
        ApplicationDbContext ctx,
        IConfiguration config,
        HttpMessageHandler handler)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(handler));
        return new VectorEmbeddingService(
            ctx,
            new Mock<ILogger<VectorEmbeddingService>>().Object,
            factory.Object,
            config);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_responder(request));
    }
}
