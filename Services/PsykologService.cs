using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UserService.Data;
using UserService.Models;

namespace UserService.Services;

public interface IPsykologService
{
    Task<PsykologSession?> StartSessionAsync(string keycloakId);
    Task<string?> SendMessageAsync(int sessionId, string keycloakId, string userMessage, CancellationToken ct = default);
    Task<PsykologSession?> EndSessionAsync(int sessionId, string keycloakId);
    Task<List<PsykologSession>> GetSessionsAsync(string keycloakId);
    Task<List<UserTheme>> GetThemesAsync(string keycloakId);
}

public class PsykologService : IPsykologService
{
    private const int FreeMonthlySessionLimit = 1;
    private const int FreeMessageLimit = 30;
    private const int PremiumMessageLimit = 50;
    private const string GroqBaseUrl = "https://api.groq.com/openai/v1/chat/completions";
    private const string GroqModel = "llama-3.3-70b-versatile";

    private const string SystemPrompt =
        "Du är en varm, professionell psykologassistent som hjälper användare på en dejting-app att " +
        "förstå sig själva bättre – sina relationsmönster, anknytningstendenser och värderingar. " +
        "Svara alltid på svenska. Ställ öppna, reflekterande frågor. Var empatisk men aldrig klinisk. " +
        "VIKTIGT: Ställ aldrig diagnos. Förskriv aldrig medicin. " +
        "Vid tecken på kris (suicidtankar, allvarlig ångest) — hänvisa till 112 eller Mind (mind.se, telefon 90101). " +
        "Avslöja inte att du är en AI om du inte direkt frågas. " +
        "Diskutera aldrig andra specifika användare. " +
        "Håll fokus: relationer, anknytning, värderingar, självinsikt.";

    private const string ThemeExtractionPrompt =
        "Analysera följande samtal och extrahera 3-7 psykologiska teman. " +
        "Returnera ENBART giltig JSON i formatet: " +
        "{\"themes\":[{\"label\":\"string\",\"intensity\":0.0-1.0,\"axis\":\"BigFive|Attachment|Values\"}]}. " +
        "Inga förklaringar, bara JSON.";

    private readonly ApplicationDbContext _db;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<PsykologService> _logger;
    private readonly IVectorEmbeddingService _vectorService;
    private readonly IConfiguration _configuration;

    public PsykologService(ApplicationDbContext db, IHttpClientFactory httpFactory, ILogger<PsykologService> logger, IVectorEmbeddingService vectorService, IConfiguration configuration)
    {
        _db = db;
        _httpFactory = httpFactory;
        _logger = logger;
        _vectorService = vectorService;
        _configuration = configuration;
    }

    public async Task<PsykologSession?> StartSessionAsync(string keycloakId)
    {
        // Monthly limit check for free users (simple: count sessions this calendar month)
        var firstOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var sessionsThisMonth = await _db.PsykologSessions
            .CountAsync(s => s.KeycloakId == keycloakId && s.StartedAt >= firstOfMonth);

        if (sessionsThisMonth >= FreeMonthlySessionLimit)
            return null; // caller returns 429

        var sessionNumber = await _db.PsykologSessions
            .CountAsync(s => s.KeycloakId == keycloakId) + 1;

        var session = new PsykologSession
        {
            KeycloakId = keycloakId,
            StartedAt = DateTime.UtcNow,
            Status = PsykologSessionStatus.Active,
            SessionNumber = sessionNumber
        };
        _db.PsykologSessions.Add(session);
        await _db.SaveChangesAsync();
        return session;
    }

    public async Task<string?> SendMessageAsync(int sessionId, string keycloakId, string userMessage, CancellationToken ct = default)
    {
        var session = await _db.PsykologSessions
            .Include(s => s.Messages)
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.KeycloakId == keycloakId && s.Status == PsykologSessionStatus.Active, ct);

        if (session == null) return null;

        var limit = FreeMessageLimit; // TODO: check premium entitlement
        if (session.Messages.Count >= limit * 2) // user + assistant pairs
            return null; // caller returns 429

        // Store user message
        session.Messages.Add(new PsykologMessage { SessionId = sessionId, Role = PsykologRole.User, Content = userMessage, CreatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync(ct);

        // Build conversation history for LLM
        var history = session.Messages
            .OrderBy(m => m.CreatedAt)
            .Select(m => new { role = m.Role == PsykologRole.User ? "user" : "assistant", content = m.Content })
            .ToList<object>();

        var assistantReply = await CallLlmAsync(SystemPrompt, history, ct)
            ?? "Förlåt, jag har lite tekniska problem just nu. Försök igen om en stund.";

        // Store assistant response
        session.Messages.Add(new PsykologMessage { SessionId = sessionId, Role = PsykologRole.Assistant, Content = assistantReply, CreatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync(ct);

        return assistantReply;
    }

    public async Task<PsykologSession?> EndSessionAsync(int sessionId, string keycloakId)
    {
        var session = await _db.PsykologSessions
            .Include(s => s.Messages)
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.KeycloakId == keycloakId && s.Status == PsykologSessionStatus.Active);

        if (session == null) return null;

        session.Status = PsykologSessionStatus.Completed;
        session.EndedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Extract themes in background (fire and forget with error handling)
        _ = ExtractThemesAsync(session);

        return session;
    }

    public Task<List<PsykologSession>> GetSessionsAsync(string keycloakId) =>
        _db.PsykologSessions
            .Where(s => s.KeycloakId == keycloakId)
            .OrderByDescending(s => s.StartedAt)
            .Select(s => new PsykologSession
            {
                Id = s.Id,
                KeycloakId = s.KeycloakId,
                StartedAt = s.StartedAt,
                EndedAt = s.EndedAt,
                ThemeCount = s.ThemeCount,
                Status = s.Status,
                SessionNumber = s.SessionNumber
            })
            .ToListAsync();

    public Task<List<UserTheme>> GetThemesAsync(string keycloakId) =>
        _db.UserThemes
            .Where(t => t.KeycloakId == keycloakId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

    // ── Internal helpers ───────────────────────────────────────────────────

    private async Task ExtractThemesAsync(PsykologSession session)
    {
        try
        {
            var conversationText = string.Join("\n", session.Messages
                .OrderBy(m => m.CreatedAt)
                .Select(m => $"{(m.Role == PsykologRole.User ? "Användare" : "Assistent")}: {m.Content}"));

            var extractionMessage = new List<object>
            {
                new { role = "user", content = $"{conversationText}\n\n{ThemeExtractionPrompt}" }
            };

            var jsonResponse = await CallLlmAsync("Du är ett JSON-verktyg. Returnera enbart giltig JSON.", extractionMessage);
            if (jsonResponse == null) return;

            using var doc = JsonDocument.Parse(jsonResponse);
            if (!doc.RootElement.TryGetProperty("themes", out var themesEl)) return;

            var themes = new List<UserTheme>();
            foreach (var t in themesEl.EnumerateArray())
            {
                themes.Add(new UserTheme
                {
                    KeycloakId = session.KeycloakId,
                    SessionId = session.Id,
                    Label = t.GetProperty("label").GetString() ?? "",
                    Intensity = t.GetProperty("intensity").GetDouble(),
                    Axis = t.GetProperty("axis").GetString() ?? "",
                    CreatedAt = DateTime.UtcNow
                });
            }

            _db.UserThemes.AddRange(themes);
            session.ThemeCount = themes.Count;

            // Purge messages after extraction (privacy by design)
            _db.PsykologMessages.RemoveRange(session.Messages);

            await _db.SaveChangesAsync();
            _logger.LogInformation("Extracted {Count} themes for session {SessionId}", themes.Count, session.Id);

            // Update the user's reflection vector (fire and forget within this task)
            try { await _vectorService.UpdateVectorAsync(session.KeycloakId); }
            catch (Exception ex) { _logger.LogWarning(ex, "Vector update failed after theme extraction"); }

            // T588: Trigger radar refresh in MatchmakingService
            try
            {
                var matchmakingUrl = _configuration["Services:MatchmakingService"] ?? "http://localhost:8083";
                var internalKey = _configuration["Services:InternalApiKey"] ?? "";
                var http = _httpFactory.CreateClient();
                http.DefaultRequestHeaders.Add("X-Internal-API-Key", internalKey);
                await http.PostAsync($"{matchmakingUrl}/api/compatibility/radar/refresh/{session.KeycloakId}", null);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Radar refresh trigger failed after psykolog session"); }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Theme extraction failed for session {SessionId}", session.Id);
        }
    }

    private async Task<string?> CallLlmAsync(string systemPrompt, List<object> messages, CancellationToken ct = default)
    {
        var apiKey = Environment.GetEnvironmentVariable("GROQ_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("GROQ_API_KEY not set — psykolog LLM unavailable");
            return null;
        }

        var payload = JsonSerializer.Serialize(new
        {
            model = GroqModel,
            messages = new List<object> { new { role = "system", content = systemPrompt } }.Concat(messages),
            max_tokens = 800,
            temperature = 0.7
        });

        try
        {
            var http = _httpFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(30);
            var req = new HttpRequestMessage(HttpMethod.Post, GroqBaseUrl)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Groq returned {Status}", resp.StatusCode);
                return null;
            }

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            return doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM call failed");
            return null;
        }
    }
}
