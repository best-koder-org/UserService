using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UserService.Data;
using UserService.Models;

namespace UserService.Services;

/// <summary>
/// T577 — Generates 128-dim reflection vectors from psykolog themes
/// and computes cosine similarity between users.
///
/// Uses a lightweight hashing approach instead of calling an external
/// embedding API (keeps it free and self-contained). For production,
/// swap GenerateEmbeddingAsync to call an OpenAI-compatible endpoint.
/// </summary>
public class VectorEmbeddingService : IVectorEmbeddingService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<VectorEmbeddingService> _logger;
    private const int VectorDim = 128;

    public VectorEmbeddingService(ApplicationDbContext context, ILogger<VectorEmbeddingService> logger, IHttpClientFactory httpFactory = null!, IConfiguration config = null!)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Update or create a reflection vector for a user based on their accumulated themes.
    /// </summary>
    public async Task<float[]> UpdateVectorAsync(string keycloakId, CancellationToken ct = default)
    {
        var themes = await _context.UserThemes
            .Where(t => t.KeycloakId == keycloakId)
            .ToListAsync(ct);

        var sessions = await _context.PsykologSessions
            .CountAsync(s => s.KeycloakId == keycloakId && s.Status == PsykologSessionStatus.Completed, ct);

        var themeSessionCount = themes.Select(t => t.SessionId).Distinct().Count();
        var effectiveSessionCount = sessions > 0 ? sessions : themeSessionCount;
        var embedding = BuildVectorFromThemes(themes, effectiveSessionCount);

        var vector = await _context.ReflectionVectors
            .FirstOrDefaultAsync(v => v.KeycloakId == keycloakId, ct);

        if (vector == null)
        {
            vector = new ReflectionVector
            {
                KeycloakId = keycloakId,
                VectorJson = SerializeVector(embedding),
                SessionCount = effectiveSessionCount,
                Confidence = CalculateConfidence(effectiveSessionCount),
                UpdatedAt = DateTime.UtcNow,
            };
            _context.ReflectionVectors.Add(vector);
        }
        else
        {
            vector.VectorJson = SerializeVector(embedding);
            vector.SessionCount = effectiveSessionCount;
            vector.Confidence = CalculateConfidence(effectiveSessionCount);
            vector.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Updated vector for {Id}: {Sessions} sessions, {Confidence:P0} confidence",
            keycloakId, sessions, vector.Confidence);

        return embedding;
    }

    /// <summary>
    /// Compute cosine similarity between two users' reflection vectors.
    /// Returns null if either user has no vector.
    /// </summary>
    public async Task<double?> CosineSimilarityAsync(string userA, string userB, CancellationToken ct = default)
    {
        var vecA = await _context.ReflectionVectors
            .FirstOrDefaultAsync(v => v.KeycloakId == userA, ct);
        var vecB = await _context.ReflectionVectors
            .FirstOrDefaultAsync(v => v.KeycloakId == userB, ct);

        if (vecA == null || vecB == null)
            return null;

        var a = DeserializeVector(vecA.VectorJson);
        var b = DeserializeVector(vecB.VectorJson);

        if (a.Length != b.Length || a.Length == 0)
            return null;

        return CosineSimilarity(a, b);
    }

    // ── Embedding generation (lightweight hash-based, no external API) ────

    public static float[] BuildVectorFromThemes(IEnumerable<UserTheme> themes, int sessionCount = 0)
    {
        var vec = new float[VectorDim];

        if (!themes.Any()) return vec;

        // Hash each theme into the vector space
        foreach (var theme in themes)
        {
            var hash = Math.Abs(HashString($"{theme.Label}:{theme.Axis}:{theme.Intensity}"));
            for (var i = 0; i < VectorDim; i++)
            {
                // Spread theme influence across dimensions using the hash
                var idx = (hash + i * 7 + i * i * 13) % VectorDim;
                vec[idx] += (float)(theme.Intensity * 0.1);
            }
        }

        // Normalize
        var magnitude = (float)Math.Sqrt(vec.Sum(v => v * v));
        if (magnitude > 0)
        {
            for (var i = 0; i < VectorDim; i++)
                vec[i] /= magnitude;
        }

        // Blend in session count signal (more sessions = stronger vector)
        if (sessionCount > 0)
        {
            var sessionFactor = Math.Min(sessionCount / 10.0, 1.0);
            for (var i = 0; i < VectorDim; i++)
                vec[i] = (float)(vec[i] * (0.5 + 0.5 * sessionFactor));

            // Re-normalize after scaling
            magnitude = (float)Math.Sqrt(vec.Sum(v => v * v));
            if (magnitude > 0)
            {
                for (var i = 0; i < VectorDim; i++)
                    vec[i] /= magnitude;
            }
        }

        return vec;
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        double dot = 0, magA = 0, magB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        var denom = Math.Sqrt(magA) * Math.Sqrt(magB);
        return denom > 0 ? dot / denom : 0;
    }

    public static double CalculateConfidence(int sessions) =>
        sessions <= 0 ? 0.0
        : sessions == 1 ? 0.4
        : sessions <= 2 ? 0.55
        : sessions == 3 ? 0.7
        : sessions <= 9 ? 0.85
        : 0.95;

    private static string SerializeVector(float[] vec) =>
        $"[{string.Join(",", vec.Select(v => v.ToString("F6")))}]";

    private static float[] DeserializeVector(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<float[]>(json) ?? Array.Empty<float>();
        }
        catch
        {
            return Array.Empty<float>();
        }
    }

    private static int HashString(string s)
    {
        unchecked
        {
            var hash = 17;
            foreach (var c in s) hash = hash * 31 + c;
            return Math.Abs(hash);
        }
    }
}
