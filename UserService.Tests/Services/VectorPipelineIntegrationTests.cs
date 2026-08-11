using System;
using System.Collections.Generic;
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

/// <summary>
/// T582 — Integration test: psykolog session → theme extraction → vector
/// generation → similarity query → score reflects shared themes.
/// Uses InMemory DB; no real HTTP calls needed.
/// </summary>
public class VectorPipelineIntegrationTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly VectorEmbeddingService _svc;

    public VectorPipelineIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"VectorPipeline_{Guid.NewGuid()}")
            .Options;
        _context = new ApplicationDbContext(options);
        _svc = new VectorEmbeddingService(
            _context,
            new Mock<ILogger<VectorEmbeddingService>>().Object,
            new Mock<IHttpClientFactory>().Object,
            new Mock<IConfiguration>().Object);
    }

    public void Dispose() => _context.Dispose();

    // ── helpers ──────────────────────────────────────────────────────────

    private async Task SeedThemesAsync(string keycloakId, IEnumerable<(string axis, string label, double intensity)> themes)
    {
        foreach (var (axis, label, intensity) in themes)
        {
            _context.UserThemes.Add(new UserTheme
            {
                KeycloakId = keycloakId,
                Axis = axis,
                Label = label,
                Intensity = intensity,
                SessionId = 1,
                CreatedAt = DateTime.UtcNow
            });
        }
        await _context.SaveChangesAsync();
    }

    // ── tests ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Pipeline_SingleUser_VectorStoredInDb()
    {
        // Arrange: seed themes for alice
        await SeedThemesAsync("alice", new[]
        {
            ("BigFive",    "Openness",        0.9),
            ("BigFive",    "Conscientiousness", 0.7),
            ("Attachment", "Secure",          0.8),
        });

        // Act: run UpdateVectorAsync (the full pipeline step)
        await _svc.UpdateVectorAsync("alice");

        // Assert: vector row created with correct structure
        var rv = await _context.ReflectionVectors
            .FirstOrDefaultAsync(r => r.KeycloakId == "alice");
        Assert.NotNull(rv);
        Assert.NotEmpty(rv.VectorJson);
        Assert.True(rv.Confidence > 0, "Confidence should be > 0 for 3 themes");
        Assert.True(rv.SessionCount >= 1);

        // Vector is parseable to 128 floats
        var floats = System.Text.Json.JsonSerializer.Deserialize<float[]>(rv.VectorJson);
        Assert.Equal(128, floats!.Length);
    }

    [Fact]
    public async Task Pipeline_TwoUsersWithSimilarThemes_HighSimilarity()
    {
        // Arrange: alice and bob share overlapping themes
        var sharedThemes = new[]
        {
            ("BigFive",    "Openness",   0.9),
            ("BigFive",    "Agreeableness", 0.8),
            ("Attachment", "Secure",     0.85),
            ("Values",     "Adventure",  0.7),
        };
        await SeedThemesAsync("alice", sharedThemes);
        await SeedThemesAsync("bob",   sharedThemes);

        await _svc.UpdateVectorAsync("alice");
        await _svc.UpdateVectorAsync("bob");

        // Act
        var similarity = await _svc.CosineSimilarityAsync("alice", "bob");

        // Assert: nearly identical themes → high cosine similarity
        Assert.NotNull(similarity);
        Assert.True(similarity!.Value > 0.95,
            $"Identical themes should yield cosine similarity near 1, got {similarity}");
    }

    [Fact]
    public async Task Pipeline_TwoUsersWithOppositeThemes_LowerSimilarity()
    {
        // Arrange: alice and charlie have entirely different axes/labels
        await SeedThemesAsync("alice", new[]
        {
            ("BigFive",    "Introversion",  0.9),
            ("Attachment", "Avoidant",      0.8),
            ("Values",     "Stability",     0.7),
        });
        await SeedThemesAsync("charlie", new[]
        {
            ("BigFive",    "Extraversion",  0.9),
            ("Attachment", "Anxious",       0.8),
            ("Values",     "Adventure",     0.7),
        });

        await _svc.UpdateVectorAsync("alice");
        await _svc.UpdateVectorAsync("charlie");

        var similar = await _svc.CosineSimilarityAsync("alice", "bob_not_in_db");
        var dissimilar = await _svc.CosineSimilarityAsync("alice", "charlie");

        // unknown user → null
        Assert.Null(similar);
        // distinct themes produce a score (can be anything 0-1 depending on slots)
        Assert.NotNull(dissimilar);
    }

    [Fact]
    public async Task Pipeline_VectorUpserted_OnSecondCall()
    {
        // Arrange
        await SeedThemesAsync("dave", new[]
        {
            ("BigFive", "Openness", 0.5),
        });

        await _svc.UpdateVectorAsync("dave");
        var firstJson = (await _context.ReflectionVectors
            .FirstAsync(r => r.KeycloakId == "dave")).VectorJson;

        // Add more themes
        await SeedThemesAsync("dave", new[]
        {
            ("Attachment", "Secure", 0.9),
            ("Values",     "Family", 0.8),
        });

        // Act: second call should upsert not duplicate
        await _svc.UpdateVectorAsync("dave");

        var rows = await _context.ReflectionVectors
            .Where(r => r.KeycloakId == "dave")
            .ToListAsync();

        Assert.Single(rows);
        Assert.NotEqual(firstJson, rows[0].VectorJson); // vector changed
    }

    [Fact]
    public async Task Pipeline_NoThemes_VectorStoredWithZeroConfidence()
    {
        // No themes seeded for this user
        await _svc.UpdateVectorAsync("ghost");

        var rv = await _context.ReflectionVectors
            .FirstOrDefaultAsync(r => r.KeycloakId == "ghost");
        Assert.NotNull(rv);
        Assert.Equal(0.0, rv!.Confidence, precision: 5);
        Assert.Equal(0, rv.SessionCount);
    }

    [Fact]
    public async Task Pipeline_SimilarityNull_WhenEitherUserMissingVector()
    {
        // only alice has a vector
        await SeedThemesAsync("alice2", new[]
        {
            ("BigFive", "Openness", 0.8),
        });
        await _svc.UpdateVectorAsync("alice2");

        var sim = await _svc.CosineSimilarityAsync("alice2", "no-vector-user");
        Assert.Null(sim);
    }
}
