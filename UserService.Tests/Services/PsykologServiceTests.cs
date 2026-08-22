using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;
using UserService.Data;
using UserService.Models;
using UserService.Services;

namespace UserService.Tests.Services;

public class PsykologServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly Mock<IHttpClientFactory> _httpFactory;
    private IPsykologService _svc;

    public PsykologServiceTests()
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"Psykolog_{Guid.NewGuid()}")
            .Options;
        _db = new ApplicationDbContext(opts);
        _httpFactory = new Mock<IHttpClientFactory>();
        _svc = CreateService(Mock.Of<IConfiguration>());
    }

    private IPsykologService CreateService(IConfiguration config, IFeatureGate? gate = null)
    {
        if (gate == null)
        {
            var g = new Mock<IFeatureGate>();
            g.Setup(x => x.IsPremium(It.IsAny<string>())).ReturnsAsync(false);
            gate = g.Object;
        }

        return new PsykologService(
            _db,
            _httpFactory.Object,
            Mock.Of<ILogger<PsykologService>>(),
            Mock.Of<IVectorEmbeddingService>(),
            config,
            gate);
    }

    private static IConfiguration Config(params (string Key, string Value)[] entries) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(entries.ToDictionary(e => e.Key, e => (string?)e.Value))
            .Build();

    public void Dispose() => _db.Dispose();

    // ── StartSession ──────────────────────────────────────────────────────

    [Fact]
    public async Task StartSession_FirstSession_ReturnsSession()
    {
        var session = await _svc.StartSessionAsync("user-1");
        Assert.NotNull(session);
        Assert.Equal("user-1", session!.KeycloakId);
        Assert.Equal(1, session.SessionNumber);
        Assert.Equal(PsykologSessionStatus.Active, session.Status);
    }

    [Fact]
    public async Task StartSession_ConfiguredLimit_BlocksWhenExceeded()
    {
        _svc = CreateService(Config(("Psykolog:FreeMonthlySessionLimit", "1")));
        Assert.NotNull(await _svc.StartSessionAsync("user-2"));
        Assert.Null(await _svc.StartSessionAsync("user-2"));
    }

    [Fact]
    public async Task StartSession_IncreasesSessionNumber()
    {
        // Simulate a session from last month so the monthly check passes
        _db.PsykologSessions.Add(new PsykologSession
        {
            KeycloakId = "user-3",
            StartedAt = DateTime.UtcNow.AddMonths(-2),
            Status = PsykologSessionStatus.Completed,
            SessionNumber = 1
        });
        await _db.SaveChangesAsync();

        var session = await _svc.StartSessionAsync("user-3");
        Assert.NotNull(session);
        Assert.Equal(2, session!.SessionNumber);
    }

    // ── SendMessage ───────────────────────────────────────────────────────

    [Fact]
    public async Task SendMessage_NoGroqKey_ReturnsFallback()
    {
        // GROQ_API_KEY not set → fallback message
        Environment.SetEnvironmentVariable("GROQ_API_KEY", null);
        var session = await _svc.StartSessionAsync("user-4");
        var reply = await _svc.SendMessageAsync(session!.Id, "user-4", "Hej!");
        // Returns the canned fallback string
        Assert.NotNull(reply);
        Assert.Contains("tekniska problem", reply);
    }

    [Fact]
    public async Task SendMessage_WrongUser_ReturnsNull()
    {
        var session = await _svc.StartSessionAsync("user-5");
        var reply = await _svc.SendMessageAsync(session!.Id, "other-user", "Hej!");
        Assert.Null(reply);
    }

    [Fact]
    public async Task SendMessage_StoresUserMessage()
    {
        Environment.SetEnvironmentVariable("GROQ_API_KEY", null);
        var session = await _svc.StartSessionAsync("user-6");
        await _svc.SendMessageAsync(session!.Id, "user-6", "Hej!");

        var messages = await _db.PsykologMessages
            .Where(m => m.SessionId == session.Id)
            .ToListAsync();
        Assert.Contains(messages, m => m.Role == PsykologRole.User && m.Content == "Hej!");
    }

    // ── EndSession ────────────────────────────────────────────────────────

    [Fact]
    public async Task EndSession_SetsCompletedStatus()
    {
        Environment.SetEnvironmentVariable("GROQ_API_KEY", null);
        var session = await _svc.StartSessionAsync("user-7");
        var ended = await _svc.EndSessionAsync(session!.Id, "user-7");

        Assert.NotNull(ended);
        Assert.Equal(PsykologSessionStatus.Completed, ended!.Status);
        Assert.NotNull(ended.EndedAt);
    }

    [Fact]
    public async Task EndSession_WrongUser_ReturnsNull()
    {
        var session = await _svc.StartSessionAsync("user-8");
        var ended = await _svc.EndSessionAsync(session!.Id, "wrong-user");
        Assert.Null(ended);
    }

    // ── GetSessions / GetThemes ───────────────────────────────────────────

    [Fact]
    public async Task GetSessions_ReturnsOnlyOwnSessions()
    {
        await _svc.StartSessionAsync("user-9");
        await _svc.StartSessionAsync("other-user");

        var sessions = await _svc.GetSessionsAsync("user-9");
        Assert.All(sessions, s => Assert.Equal("user-9", s.KeycloakId));
    }

    [Fact]
    public async Task GetThemes_ReturnsOnlyOwnThemes()
    {
        _db.UserThemes.AddRange(
            new UserTheme { KeycloakId = "user-10", SessionId = 1, Label = "openness", Intensity = 0.8, Axis = "BigFive" },
            new UserTheme { KeycloakId = "other-user", SessionId = 2, Label = "anxiety", Intensity = 0.6, Axis = "Attachment" }
        );
        await _db.SaveChangesAsync();

        var themes = await _svc.GetThemesAsync("user-10");
        Assert.Single(themes);
        Assert.Equal("openness", themes[0].Label);
    }

    // ── Limits: configurable + premium ─────────────────────────────────────

    [Fact]
    public async Task StartSession_DefaultLimit_AllowsMoreThanOne()
    {
        // Default config: 5 free sessions/month — open to everyone now.
        Assert.NotNull(await _svc.StartSessionAsync("u-free"));
        Assert.NotNull(await _svc.StartSessionAsync("u-free"));
    }

    [Fact]
    public async Task StartSession_PremiumBypassesFreeLimit()
    {
        var gate = new Mock<IFeatureGate>();
        gate.Setup(g => g.IsPremium(It.IsAny<string>())).ReturnsAsync(true);
        _svc = CreateService(Config(("Psykolog:FreeMonthlySessionLimit", "1")), gate.Object);

        Assert.NotNull(await _svc.StartSessionAsync("u-prem"));
        Assert.NotNull(await _svc.StartSessionAsync("u-prem")); // not blocked at free limit 1
    }

    [Fact]
    public async Task SendMessage_RespectsConfiguredMessageLimit()
    {
        Environment.SetEnvironmentVariable("GROQ_API_KEY", null);
        _svc = CreateService(Config(("Psykolog:FreeMessageLimit", "1")));
        var s = await _svc.StartSessionAsync("u-msg");
        Assert.NotNull(await _svc.SendMessageAsync(s!.Id, "u-msg", "Hej"));
        // 2 stored (user+assistant) >= limit*2 → blocked
        Assert.Null(await _svc.SendMessageAsync(s!.Id, "u-msg", "Hej igen"));
    }

    [Fact]
    public async Task SendMessage_PremiumUsesPremiumLimit()
    {
        Environment.SetEnvironmentVariable("GROQ_API_KEY", null);
        var gate = new Mock<IFeatureGate>();
        gate.Setup(g => g.IsPremium(It.IsAny<string>())).ReturnsAsync(true);
        _svc = CreateService(Config(("Psykolog:FreeMessageLimit", "1"), ("Psykolog:PremiumMessageLimit", "10")), gate.Object);
        var s = await _svc.StartSessionAsync("u-prem-msg");
        Assert.NotNull(await _svc.SendMessageAsync(s!.Id, "u-prem-msg", "Hej"));
        Assert.NotNull(await _svc.SendMessageAsync(s!.Id, "u-prem-msg", "Hej igen"));
    }

    // ── Transcript (re-read) ──────────────────────────────────────────────

    [Fact]
    public async Task GetMessages_ReturnsOwnTranscript()
    {
        Environment.SetEnvironmentVariable("GROQ_API_KEY", null);
        var s = await _svc.StartSessionAsync("u-transcript");
        await _svc.SendMessageAsync(s!.Id, "u-transcript", "Jag känner mig ensam");

        var messages = await _svc.GetMessagesAsync(s.Id, "u-transcript");
        Assert.NotNull(messages);
        Assert.Contains(messages!, m => m.Role == PsykologRole.User && m.Content == "Jag känner mig ensam");
        Assert.Contains(messages!, m => m.Role == PsykologRole.Assistant);
    }

    [Fact]
    public async Task GetMessages_WrongUser_ReturnsNull()
    {
        Environment.SetEnvironmentVariable("GROQ_API_KEY", null);
        var s = await _svc.StartSessionAsync("u-owner");
        Assert.Null(await _svc.GetMessagesAsync(s!.Id, "intruder"));
    }

    // ── Stale session expiry ──────────────────────────────────────────────

    [Fact]
    public async Task GetSessions_ExpiresStaleActiveSession()
    {
        _svc = CreateService(Config(("Psykolog:MaxSessionAgeMinutes", "0")));
        _db.PsykologSessions.Add(new PsykologSession
        {
            KeycloakId = "u-stale",
            StartedAt = DateTime.UtcNow.AddMinutes(-5),
            Status = PsykologSessionStatus.Active,
            SessionNumber = 1
        });
        await _db.SaveChangesAsync();

        var sessions = await _svc.GetSessionsAsync("u-stale");
        Assert.All(sessions, s => Assert.Equal(PsykologSessionStatus.Completed, s.Status));
    }

    // ── Bio audit (dating coach) ──────────────────────────────────────────

    [Fact]
    public async Task BioAudit_NoThemes_ReturnsNull()
    {
        Environment.SetEnvironmentVariable("GROQ_API_KEY", null);
        Assert.Null(await _svc.BioAuditAsync("u-nobio-themes"));
    }

    [Fact]
    public async Task BioAudit_NoBio_ReturnsNull()
    {
        Environment.SetEnvironmentVariable("GROQ_API_KEY", null);
        _db.UserThemes.Add(new UserTheme
        {
            KeycloakId = "u-nobio", Label = "Openness", Axis = "BigFive",
            Intensity = 0.8, SessionId = 1, CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        Assert.Null(await _svc.BioAuditAsync("u-nobio"));
    }

    [Fact]
    public async Task BioAudit_NoGroqKey_ReturnsNull()
    {
        Environment.SetEnvironmentVariable("GROQ_API_KEY", null);
        var userId = Guid.NewGuid();
        _db.UserThemes.Add(new UserTheme
        {
            KeycloakId = userId.ToString(), Label = "Openness", Axis = "BigFive",
            Intensity = 0.8, SessionId = 1, CreatedAt = DateTime.UtcNow
        });
        _db.UserProfiles.Add(new UserProfile { UserId = userId, Bio = "Jag gillar att resa och umgås med vänner." });
        await _db.SaveChangesAsync();

        Assert.Null(await _svc.BioAuditAsync(userId.ToString()));
    }
}
