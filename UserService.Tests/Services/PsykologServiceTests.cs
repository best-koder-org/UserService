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
    private readonly IPsykologService _svc;

    public PsykologServiceTests()
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"Psykolog_{Guid.NewGuid()}")
            .Options;
        _db = new ApplicationDbContext(opts);
        _httpFactory = new Mock<IHttpClientFactory>();
        _svc = new PsykologService(_db, _httpFactory.Object, Mock.Of<ILogger<PsykologService>>(), Mock.Of<IVectorEmbeddingService>(), Mock.Of<IConfiguration>());
    }

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
    public async Task StartSession_SecondSessionSameMonth_ReturnsNull()
    {
        // First session succeeds
        var first = await _svc.StartSessionAsync("user-2");
        Assert.NotNull(first);

        // Second session this month blocked (free limit = 1)
        var second = await _svc.StartSessionAsync("user-2");
        Assert.Null(second);
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
}
