using System;
using System.Collections.Generic;
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

    public void Dispose() => _context.Dispose();
}
