using System;
using System.Net;
using System.Net.Http;
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

public class AccountDeletionServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<HttpMessageHandler> _httpHandlerMock;
    private readonly Mock<ILogger<AccountDeletionService>> _loggerMock;
    private readonly IConfiguration _configuration;
    private readonly AccountDeletionService _service;

    public AccountDeletionServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"AccountDeletion_{Guid.NewGuid()}")
            .Options;
        _context = new ApplicationDbContext(options);

        _httpHandlerMock = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(_httpHandlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:8080")
        };

        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        _loggerMock = new Mock<ILogger<AccountDeletionService>>();

        var configData = new Dictionary<string, string?>
        {
            { "Gateway:BaseUrl", "http://localhost:8080" }
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        _service = new AccountDeletionService(
            _context,
            _httpClientFactoryMock.Object,
            _loggerMock.Object,
            _configuration);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private UserProfile CreateTestUser(int id = 1)
    {
        var user = new UserProfile
        {
            Id = id,
            UserId = Guid.NewGuid(),
            Name = "Test User",
            Email = "test@example.com",
            Bio = "A test bio",
            Gender = "Male",
            DateOfBirth = new DateTime(1990, 1, 1),
            IsActive = true
        };
        _context.UserProfiles.Add(user);
        _context.SaveChanges();
        return user;
    }

    private void SetupHttpResponse(string urlPattern, HttpStatusCode statusCode, string content = "0")
    {
        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.PathAndQuery.Contains(urlPattern)),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content)
            });
    }

    private void SetupAllDownstreamSuccess(int photos = 3, int matches = 2, int messages = 5, int swipes = 10, string safety = "1,2")
    {
        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.PathAndQuery.Contains("/api/photos/user/")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(photos.ToString()) });

        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.PathAndQuery.Contains("/api/matchmaking/user/")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(matches.ToString()) });

        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.PathAndQuery.Contains("/api/messages/user/")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(messages.ToString()) });

        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.PathAndQuery.Contains("/api/swipes/user/")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(swipes.ToString()) });

        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.PathAndQuery.Contains("/api/safety/user/")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(safety) });
    }

    // ===== User Not Found =====

    [Fact]
    public async Task DeleteAccount_UserNotFound_ReturnsFalse()
    {
        var result = await _service.DeleteAccountAsync(999);

        Assert.False(result.Success);
        Assert.Equal("User profile not found", result.Message);
    }

    // ===== Soft Delete =====

    [Fact]
    public async Task DeleteAccount_SoftDelete_DeactivatesProfile()
    {
        var user = CreateTestUser();
        SetupAllDownstreamSuccess();

        var result = await _service.DeleteAccountAsync(user.Id, hardDelete: false);

        Assert.True(result.Success);
        Assert.Contains("deactivated", result.Message);

        var updatedUser = await _context.UserProfiles.FindAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.False(updatedUser.IsActive);
    }

    [Fact]
    public async Task DeleteAccount_SoftDelete_ScramblesPersonalData()
    {
        var user = CreateTestUser();
        SetupAllDownstreamSuccess();

        await _service.DeleteAccountAsync(user.Id, hardDelete: false);

        var updatedUser = await _context.UserProfiles.FindAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal("[Deleted User]", updatedUser.Name);
        Assert.Contains("@deleted.local", updatedUser.Email);
        Assert.Equal("", updatedUser.Bio);
    }

    [Fact]
    public async Task DeleteAccount_SoftDelete_UpdatesTimestamp()
    {
        var user = CreateTestUser();
        SetupAllDownstreamSuccess();
        var beforeDeletion = DateTime.UtcNow;

        await _service.DeleteAccountAsync(user.Id, hardDelete: false);

        var updatedUser = await _context.UserProfiles.FindAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.True(updatedUser.UpdatedAt >= beforeDeletion);
    }

    // ===== Hard Delete =====

    [Fact]
    public async Task DeleteAccount_HardDelete_RemovesProfile()
    {
        var user = CreateTestUser();
        SetupAllDownstreamSuccess();

        var result = await _service.DeleteAccountAsync(user.Id, hardDelete: true);

        Assert.True(result.Success);
        Assert.Contains("permanently deleted", result.Message);

        var deletedUser = await _context.UserProfiles.FindAsync(user.Id);
        Assert.Null(deletedUser);
    }

    // ===== Summary Counts =====

    [Fact]
    public async Task DeleteAccount_SuccessfulDeletion_ReturnsCorrectCounts()
    {
        var user = CreateTestUser();
        SetupAllDownstreamSuccess(photos: 5, matches: 3, messages: 12, swipes: 20, safety: "4,6");

        var result = await _service.DeleteAccountAsync(user.Id);

        Assert.True(result.Success);
        Assert.Equal(5, result.Summary.PhotosDeleted);
        Assert.Equal(3, result.Summary.MatchesDeleted);
        Assert.Equal(12, result.Summary.MessagesDeleted);
        Assert.Equal(20, result.Summary.SwipesDeleted);
        Assert.Equal(4, result.Summary.SafetyReportsDeleted);
        Assert.Equal(6, result.Summary.BlocksDeleted);
        Assert.True(result.Summary.ProfileDeleted);
        Assert.NotEqual(default, result.Summary.DeletedAt);
    }

    // ===== Partial Failures =====

    [Fact]
    public async Task DeleteAccount_PhotoServiceFails_StillDeletesOtherData()
    {
        var user = CreateTestUser();

        // Photo service returns 500, rest succeed
        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.PathAndQuery.Contains("/api/photos/user/")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.InternalServerError });

        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.PathAndQuery.Contains("/api/matchmaking/user/")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent("2") });

        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.PathAndQuery.Contains("/api/messages/user/")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent("5") });

        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.PathAndQuery.Contains("/api/swipes/user/")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent("10") });

        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.PathAndQuery.Contains("/api/safety/user/")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent("1,2") });

        var result = await _service.DeleteAccountAsync(user.Id);

        Assert.True(result.Success);
        Assert.Equal(0, result.Summary.PhotosDeleted); // Failed
        Assert.Equal(2, result.Summary.MatchesDeleted); // Succeeded
        Assert.Equal(5, result.Summary.MessagesDeleted); // Succeeded
    }

    [Fact]
    public async Task DeleteAccount_DownstreamException_ReturnsZeroCount()
    {
        var user = CreateTestUser();

        // Photo service throws exception
        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.PathAndQuery.Contains("/api/photos/user/")),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        // Rest succeed
        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => !r.RequestUri!.PathAndQuery.Contains("/api/photos/user/")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent("0") });

        var result = await _service.DeleteAccountAsync(user.Id);

        Assert.True(result.Success);
        Assert.Equal(0, result.Summary.PhotosDeleted);
    }

    // ===== Preferences Deletion =====

    [Fact]
    public async Task DeleteAccount_DeletesMatchPreferences()
    {
        var user = CreateTestUser();
        _context.MatchPreferences.Add(new MatchPreferences
        {
            UserProfileId = user.Id,
            UserId = user.UserId,
            MinAge = 21,
            MaxAge = 35
        });
        _context.SaveChanges();
        SetupAllDownstreamSuccess();

        await _service.DeleteAccountAsync(user.Id, hardDelete: true);

        var prefs = await _context.MatchPreferences.FirstOrDefaultAsync(p => p.UserProfileId == user.Id);
        Assert.Null(prefs);
    }

    [Fact]
    public async Task DeleteAccount_NoPreferences_DoesNotThrow()
    {
        var user = CreateTestUser();
        SetupAllDownstreamSuccess();

        var result = await _service.DeleteAccountAsync(user.Id);

        Assert.True(result.Success);
    }

    // ===== Safety Data Parsing =====

    [Fact]
    public async Task DeleteAccount_SafetyData_ParsesCommaSeparatedResponse()
    {
        var user = CreateTestUser();
        SetupAllDownstreamSuccess(safety: "7,3");

        var result = await _service.DeleteAccountAsync(user.Id);

        Assert.Equal(7, result.Summary.SafetyReportsDeleted);
        Assert.Equal(3, result.Summary.BlocksDeleted);
    }

    [Fact]
    public async Task DeleteAccount_SafetyData_MalformedResponse_ReturnsZeros()
    {
        var user = CreateTestUser();

        // Setup all normal but override safety with malformed
        SetupAllDownstreamSuccess();
        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.PathAndQuery.Contains("/api/safety/user/")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent("invalid") });

        var result = await _service.DeleteAccountAsync(user.Id);

        Assert.True(result.Success);
        Assert.Equal(0, result.Summary.SafetyReportsDeleted);
        Assert.Equal(0, result.Summary.BlocksDeleted);
    }

    // ===== Non-parseable downstream responses =====

    [Fact]
    public async Task DeleteAccount_NonNumericResponse_ReturnsZeroCount()
    {
        var user = CreateTestUser();
        SetupAllDownstreamSuccess();

        // Override photos response with non-numeric
        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.PathAndQuery.Contains("/api/photos/user/")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent("not_a_number") });

        var result = await _service.DeleteAccountAsync(user.Id);

        Assert.True(result.Success);
        Assert.Equal(0, result.Summary.PhotosDeleted);
    }
}
