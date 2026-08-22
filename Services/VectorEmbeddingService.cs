using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UserService.Data;
using UserService.Models;

namespace UserService.Services;

/// <summary>
/// T577 — Generates reflection vectors from psykolog themes and computes
/// cosine similarity between users.
///
/// Two paths:
///   1. REAL embeddings (P1): when explicitly enabled via "Embeddings:Enabled":
///      true (with an EMBEDDINGS_API_KEY or Embeddings:ApiKey), themes are
///      embedded through an OpenAI-compatible /embeddings endpoint, producing
///      semantically meaningful vectors at the provider's native dimension.
///   2. HASH fallback (dev/demo, no provider configured): a deterministic
///      128-dim bag-of-slots hash so the pipeline always yields a vector.
/// When embeddings are expected but unavailable (no key / API failure) the
/// vector is SKIPPED (empty array, nothing persisted) so no noise enters the
/// 40% compatibility blend — CosineSimilarityAsync returns null in that case.
/// </summary>
public class VectorEmbeddingService : IVectorEmbeddingService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<VectorEmbeddingService> _logger;
    private readonly IHttpClientFactory? _httpFactory;
    private readonly IConfiguration? _config;
    private const int VectorDim = 128;

    public VectorEmbeddingService(ApplicationDbContext context, ILogger<VectorEmbeddingService> logger, IHttpClientFactory httpFactory = null!, IConfiguration config = null!)
    {
        _context = context;
        _logger = logger;
        _httpFactory = httpFactory;
        _config = config;
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
        var embedding = await GenerateEmbeddingAsync(themes, effectiveSessionCount, ct);

        // Embeddings configured but unavailable (no key / API failure): skip
        // instead of persisting noise. CosineSimilarityAsync returns null when
        // no vector exists, so the 40% compatibility blend simply does not apply.
        if (embedding.Length == 0)
        {
            _logger.LogWarning("Skipping vector update for {Id}: embeddings enabled but unavailable", keycloakId);
            return embedding;
        }

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

    // ── Embedding generation ──────────────────────────────────────────────

    /// <summary>
    /// Produce the reflection vector. Uses a real embedding API when configured
    /// (see class doc), otherwise the deterministic hash fallback. Returns an
    /// empty array when embeddings are expected but unavailable — the caller
    /// skips persistence so no noise enters the 40% compatibility blend.
    /// </summary>
    private async Task<float[]> GenerateEmbeddingAsync(List<UserTheme> themes, int sessionCount, CancellationToken ct)
    {
        if (!IsEmbeddingEnabled)
            return BuildVectorFromThemes(themes, sessionCount);

        var apiKey = EmbeddingApiKey;
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogError("Embeddings enabled but no API key (EMBEDDINGS_API_KEY / Embeddings:ApiKey) — vector skipped");
            return Array.Empty<float>();
        }

        if (themes.Count == 0)
            return Array.Empty<float>();

        try
        {
            var vec = await EmbedThemesAsync(themes, apiKey, ct);
            if (vec.Length == 0)
            {
                _logger.LogWarning("Embedding endpoint returned no vector — skipping");
                return Array.Empty<float>();
            }
            return Normalize(vec);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Embedding call failed — vector skipped (no blend for this user)");
            return Array.Empty<float>();
        }
    }

    /// <summary>
    /// Calls an OpenAI-compatible POST {baseUrl}/embeddings with the themes as
    /// a single input string. Returns the provider vector at its native
    /// dimension (any length); empty array on any failure. Storage is
    /// <c>mediumtext</c> and cosine handles arbitrary equal-length vectors, so
    /// no forced dimension is needed.
    /// </summary>
    private async Task<float[]> EmbedThemesAsync(List<UserTheme> themes, string apiKey, CancellationToken ct)
    {
        if (_httpFactory == null) return Array.Empty<float>();

        var baseUrl = (EmbeddingBaseUrl ?? "https://api.openai.com/v1").TrimEnd('/');
        var payload = JsonSerializer.Serialize(new
        {
            model = EmbeddingModel,
            input = BuildEmbeddingText(themes)
        });

        var http = _httpFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(EmbeddingTimeoutSeconds);
        var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/embeddings")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var resp = await http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("Embedding endpoint returned {Status}", (int)resp.StatusCode);
            return Array.Empty<float>();
        }

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.GetArrayLength() == 0)
            return Array.Empty<float>();

        var floats = new List<float>();
        foreach (var v in data[0].GetProperty("embedding").EnumerateArray())
            floats.Add(v.GetSingle());
        return floats.ToArray();
    }

    /// <summary>Compact input text for the embedding model, e.g. "Openness (BigFive) intensity 0.8; ..."</summary>
    private static string BuildEmbeddingText(IEnumerable<UserTheme> themes) =>
        string.Join("; ", themes.Select(t => $"{t.Label} ({t.Axis}) intensity {t.Intensity:F1}"));

    private static float[] Normalize(float[] vec)
    {
        var magnitude = (float)Math.Sqrt(vec.Sum(v => (double)v * v));
        if (magnitude <= 0) return vec;
        for (var i = 0; i < vec.Length; i++) vec[i] /= magnitude;
        return vec;
    }

    // ── Configuration ─────────────────────────────────────────────────────

    /// <summary>
    /// Embeddings are used only when explicitly enabled via
    /// "Embeddings:Enabled": true. The API key may come from the
    /// EMBEDDINGS_API_KEY environment variable or "Embeddings:ApiKey"
    /// (never checked into appsettings).
    /// </summary>
    private bool IsEmbeddingEnabled =>
        _config != null
        && bool.TryParse(_config["Embeddings:Enabled"], out var enabled)
        && enabled;

    private string EmbeddingApiKey =>
        Environment.GetEnvironmentVariable("EMBEDDINGS_API_KEY")
        ?? _config?["Embeddings:ApiKey"]
        ?? string.Empty;

    private string EmbeddingBaseUrl => _config?["Embeddings:BaseUrl"] ?? "https://api.openai.com/v1";

    private string EmbeddingModel => _config?["Embeddings:Model"] ?? "text-embedding-3-small";

    private int EmbeddingTimeoutSeconds =>
        int.TryParse(_config?["Embeddings:TimeoutSeconds"], out var t) ? t : 20;

    // ── Hash fallback (dev/demo, no provider configured) ──────────────────

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
