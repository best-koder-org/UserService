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
    Task<List<PsykologMessage>?> GetMessagesAsync(int sessionId, string keycloakId);
    Task<List<string>?> BioAuditAsync(string keycloakId);
}

public class PsykologService : IPsykologService
{
    // Limits come from configuration (Psykolog:* section) so they can be tuned
    // per environment and gated behind premium later. Generous defaults keep the
    // feature open to everyone now; premium flips to the higher set automatically.
    private int FreeMonthlySessionLimit => ReadInt("Psykolog:FreeMonthlySessionLimit", 5);
    private int PremiumMonthlySessionLimit => ReadInt("Psykolog:PremiumMonthlySessionLimit", 0); // 0 = unlimited
    private int FreeMessageLimit => ReadInt("Psykolog:FreeMessageLimit", 30);
    private int PremiumMessageLimit => ReadInt("Psykolog:PremiumMessageLimit", 60);
    private const string GroqBaseUrl = "https://api.groq.com/openai/v1/chat/completions";
    private const string GroqModel = "llama-3.3-70b-versatile";

    private static readonly Dictionary<string, string> AxisSuggestions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["emotionalStability"] = "Användaren har låg emotionell stabilitet. Föreslå övningar i känsloreglering. Fråga om stresshantering.",
        ["socialEnergy"] = "Användaren har låg social energi. Föreslå strategier för social återhämtning. Fråga om sociala situationer.",
        ["openness"] = "Användaren har låg öppenhet. Uppmuntra utforskande av nya perspektiv. Fråga om rädsla för sårbarhet.",
        ["warmth"] = "Användaren har låg värme. Föreslå övningar i empati och kontakt. Fråga om känslomässig närhet.",
        ["lifeStructure"] = "Användaren har låg struktur. Fråga om rutiner, balans och organisering.",
        ["intimacyComfort"] = "Användaren har låg intimitetskomfort. Föreslå trappsteg för att bygga tillit. Fråga om rädsla för närhet.",
        ["conflictStyle"] = "Användaren har låg konfliktstil. Föreslå tekniker för konstruktiv konflikthantering. Fråga om undvikandemönster.",
    };

    private static readonly Dictionary<string, string> AxisSwedishLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["emotionalStability"] = "känslomässig stabilitet",
        ["socialEnergy"] = "social energi",
        ["openness"] = "öppenhet",
        ["warmth"] = "värme",
        ["lifeStructure"] = "livsstruktur",
        ["intimacyComfort"] = "intimitetskomfort",
        ["conflictStyle"] = "konfliktstil",
    };
    private const string SystemPrompt =
        "Du är en varm, professionell relationsexpert och dating-coach som hjälper användare på en dejting-app " +
        "att (1) förstå sig själva bättre — sina relationsmönster, anknytningstendenser och värderingar — och " +
        "(2) utforska vilken typ av partner de letar efter. Svara alltid på svenska. Ställ öppna, reflekterande " +
        "frågor, en i taget. Var empatisk, konkret och coachande, men aldrig klinisk. " +
        "VIKTIGT: Ställ aldrig diagnos. Förskriv aldrig medicin. " +
        "Vid tecken på kris (suicidtankar, allvarlig ångest) — hänvisa till 112 eller Mind (mind.se, telefon 90101). " +
        "Avslöja inte att du är en AI om du inte direkt frågas. " +
        "Diskutera aldrig andra specifika användare. " +
        "Håll fokus: relationer, anknytning, värderingar, självinsikt och partnerpreferenser.";

    private static string BuildSystemPrompt(PsykologSession session)
    {
        var prompt = SystemPrompt;

        if (string.IsNullOrEmpty(session.WeakestAxesJson))
            return prompt;

        try
        {
            var axes = JsonSerializer.Deserialize<List<string>>(session.WeakestAxesJson);
            if (axes == null || axes.Count == 0) return prompt;

            var suggestions = new List<string>();
            foreach (var axis in axes)
            {
                var cleanAxis = axis.Trim().ToLowerInvariant();
                if (AxisSuggestions.TryGetValue(cleanAxis, out var suggestion))
                {
                    suggestions.Add(suggestion);
                }
            }

            if (suggestions.Count > 0)
            {
                prompt += "\n\n[KONTEXTUELLA FÖRSLAG baserat på användarens svagaste radar-områden:]\n";
                prompt += string.Join("\n", suggestions);
            }
        }
        catch { /* gracelfull degradering */ }

        return prompt;
    }

    private const string ThemeExtractionPrompt =
        "Analysera följande samtal och extrahera 3-7 psykologiska teman. " +
        "Teman om ANVÄNDAREN SJÄLV använder axis \"BigFive\", \"Attachment\" eller \"Values\". " +
        "Teman om VILKEN PARTNER användaren letar efter använder axis \"PartnerValue\", \"PartnerAttachment\" eller \"PartnerTrait\" (label beskriver egenskapen/önskemålet). " +
        "Returnera ENBART giltig JSON i formatet: " +
        "{\"themes\":[{\"label\":\"string\",\"intensity\":0.0-1.0,\"axis\":\"string\"}]}. " +
        "Inga förklaringar, bara JSON.";

    private readonly ApplicationDbContext _db;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<PsykologService> _logger;
    private readonly IVectorEmbeddingService _vectorService;
    private readonly IConfiguration _configuration;
    private readonly IFeatureGate _featureGate;

    public PsykologService(
        ApplicationDbContext db,
        IHttpClientFactory httpFactory,
        ILogger<PsykologService> logger,
        IVectorEmbeddingService vectorService,
        IConfiguration configuration,
        IFeatureGate featureGate)
    {
        _db = db;
        _httpFactory = httpFactory;
        _logger = logger;
        _vectorService = vectorService;
        _configuration = configuration;
        _featureGate = featureGate;
    }

    private int ReadInt(string key, int def) =>
        int.TryParse(_configuration[key], out var v) ? v : def;

    public async Task<PsykologSession?> StartSessionAsync(string keycloakId)
    {
        // Expire any stale abandoned sessions first (so they extract themes once)
        await ExpireStaleSessionsAsync(keycloakId);

        // Monthly limit check (configurable; premium can be unlimited)
        var premium = await _featureGate.IsPremium(keycloakId);
        var monthlyLimit = premium ? PremiumMonthlySessionLimit : FreeMonthlySessionLimit;

        var firstOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var sessionsThisMonth = await _db.PsykologSessions
            .CountAsync(s => s.KeycloakId == keycloakId && s.StartedAt >= firstOfMonth);

        if (monthlyLimit > 0 && sessionsThisMonth >= monthlyLimit)
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

        // T633: Fetch radar profile and store weakest axes
        _ = IdentifyWeakestAxesAsync(session);

        return session;
    }

    public async Task<string?> SendMessageAsync(int sessionId, string keycloakId, string userMessage, CancellationToken ct = default)
    {
        var session = await _db.PsykologSessions
            .Include(s => s.Messages)
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.KeycloakId == keycloakId && s.Status == PsykologSessionStatus.Active, ct);

        if (session == null) return null;

        var premium = await _featureGate.IsPremium(keycloakId);
        var limit = premium ? PremiumMessageLimit : FreeMessageLimit;
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

        var assistantReply = await CallLlmAsync(BuildSystemPrompt(session), history, ct)
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

    public async Task<List<PsykologSession>> GetSessionsAsync(string keycloakId)
    {
        // Lazily expire stale active sessions so abandoned sessions still
        // extract themes once and don't linger as "active" forever.
        await ExpireStaleSessionsAsync(keycloakId);

        return await _db.PsykologSessions
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
    }

    /// <summary>Re-read a session's full transcript (owner only). Null if not found/not owned.</summary>
    public async Task<List<PsykologMessage>?> GetMessagesAsync(int sessionId, string keycloakId)
    {
        var owned = await _db.PsykologSessions
            .AnyAsync(s => s.Id == sessionId && s.KeycloakId == keycloakId);
        if (!owned) return null;

        return await _db.PsykologMessages
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Dating-coach: compare the user's extracted themes against their profile bio
    /// and return kind, concrete suggestions (recommendation only — never edits).
    /// Returns null when there are no themes, no bio, or no LLM key.
    /// </summary>
    public async Task<List<string>?> BioAuditAsync(string keycloakId)
    {
        var themes = await _db.UserThemes
            .Where(t => t.KeycloakId == keycloakId && !t.Axis.StartsWith("Partner"))
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
        if (themes.Count == 0) return null;

        string? bio = null;
        if (Guid.TryParse(keycloakId, out var gid))
        {
            var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == gid);
            bio = profile?.Bio;
        }
        if (string.IsNullOrWhiteSpace(bio)) return null;

        var apiKey = Environment.GetEnvironmentVariable("GROQ_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("GROQ_API_KEY not set — bio audit unavailable");
            return null;
        }

        var themeText = string.Join("; ", themes.Select(t => $"{t.Label} ({t.Axis}) {t.Intensity:F1}"));
        var prompt =
            $"Användarens utvunna psykologiska teman: {themeText}\n\n" +
            $"Användarens nuvarande bio: \"{bio}\"\n\n" +
            "Analysera om bio:n speglar vem användaren är. Returnera ENBART giltig JSON i formatet " +
            "{\"suggestions\":[\"string\"]} med 3-5 konkreta, vänliga och handlingsbara förslag på svenska. " +
            "Kategorier: saknas (ett starkt tema som inte syns i bio:n), motsägelse (bio:n säger X men teman antyder Y), klyscha (generisk fras).";

        var messages = new List<object> { new { role = "user", content = prompt } };
        var jsonResponse = await CallLlmAsync("Du är en relationsexpert och dating-coach. Returnera enbart giltig JSON.", messages);
        if (jsonResponse == null) return null;

        try
        {
            using var doc = JsonDocument.Parse(jsonResponse);
            if (!doc.RootElement.TryGetProperty("suggestions", out var arr)) return null;
            return arr.EnumerateArray()
                .Select(x => x.GetString())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Cast<string>()
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Bio audit JSON parse failed");
            return null;
        }
    }

    /// <summary>
    /// Marks Active sessions older than Psykolog:MaxSessionAgeMinutes (default
    /// 6h) as Completed and kicks off theme extraction once. Keeps abandoned
    /// sessions from lingering and still feeds the vector pipeline.
    /// </summary>
    private async Task ExpireStaleSessionsAsync(string keycloakId)
    {
        var maxAge = TimeSpan.FromMinutes(ReadInt("Psykolog:MaxSessionAgeMinutes", 360));
        var cutoff = DateTime.UtcNow.Subtract(maxAge);

        var stale = await _db.PsykologSessions
            .Where(s => s.KeycloakId == keycloakId
                && s.Status == PsykologSessionStatus.Active
                && s.StartedAt < cutoff)
            .ToListAsync();

        if (stale.Count == 0) return;

        foreach (var s in stale)
        {
            s.Status = PsykologSessionStatus.Completed;
            s.EndedAt = DateTime.UtcNow;
            _logger.LogInformation("Auto-expiring stale psykolog session {Id} for {User}", s.Id, keycloakId);
        }
        await _db.SaveChangesAsync();

        foreach (var s in stale)
            _ = ExtractThemesAsync(s);
    }

    public Task<List<UserTheme>> GetThemesAsync(string keycloakId) =>
        _db.UserThemes
            .Where(t => t.KeycloakId == keycloakId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

    // ── Internal helpers ───────────────────────────────────────────────────

    /// <summary>
    /// T633: Fetches user's radar profile from MatchmakingService and identifies
    /// the 3 weakest axes. Stores as JSON on the session for prompt injection.
    /// </summary>
    private async Task IdentifyWeakestAxesAsync(PsykologSession session)
    {
        try
        {
            var matchmakingUrl = _configuration["Services:MatchmakingService"] ?? "http://localhost:8083";
            var http = _httpFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(5);

            var resp = await http.GetAsync($"{matchmakingUrl}/api/compatibility/radar/{session.KeycloakId}");
            if (!resp.IsSuccessStatusCode) return;

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var axesEl = doc.RootElement.GetProperty("axes");

            var axes = new List<(string Name, double Value)>
            {
                ("emotionalStability", axesEl.GetProperty("emotionalStability").GetDouble()),
                ("socialEnergy", axesEl.GetProperty("socialEnergy").GetDouble()),
                ("openness", axesEl.GetProperty("openness").GetDouble()),
                ("warmth", axesEl.GetProperty("warmth").GetDouble()),
                ("lifeStructure", axesEl.GetProperty("lifeStructure").GetDouble()),
                ("intimacyComfort", axesEl.GetProperty("intimacyComfort").GetDouble()),
                ("conflictStyle", axesEl.GetProperty("conflictStyle").GetDouble()),
            };

            // Pick bottom 3 by value (weakest axes)
            var weakest = axes.OrderBy(a => a.Value).Take(3).Select(a => a.Name).ToList();

            session.WeakestAxesJson = JsonSerializer.Serialize(weakest);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Identified weakest axes for {User}: {Axes}",
                session.KeycloakId, string.Join(", ", weakest));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to fetch radar profile for axis-based suggestions (T633)");
            // Graceful degradation — no axis suggestions this session
        }
    }

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

            // Transcript is intentionally KEPT so users can re-read past
            // reflections ("Din resa"). Session deletion (GDPR) is handled
            // separately; themes + vectors drive matching.
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
