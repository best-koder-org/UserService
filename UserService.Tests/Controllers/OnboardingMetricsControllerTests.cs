using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using UserService.Controllers;
using UserService.Data;
using UserService.DTOs;
using UserService.Models;

namespace UserService.Tests.Controllers;

/// <summary>
/// Tests for OnboardingMetricsController — aggregate onboarding funnel metrics.
/// Uses real InMemory ApplicationDbContext to seed profiles and count them.
/// </summary>
public class OnboardingMetricsControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ILogger<OnboardingMetricsController>> _mockLogger;
    private readonly OnboardingMetricsController _controller;

    public OnboardingMetricsControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"OnboardingMetricsTests_{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);
        _mockLogger = new Mock<ILogger<OnboardingMetricsController>>();
        _controller = new OnboardingMetricsController(_context, _mockLogger.Object);

        // Auth required by [Authorize] attribute — set a valid user
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim("sub", Guid.NewGuid().ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private async Task<UserProfile> SeedProfile(
        string name, string gender, DateTime dob,
        int photoCount = 0,
        OnboardingStatus status = OnboardingStatus.Incomplete,
        bool addPreferences = false)
    {
        var userId = Guid.NewGuid();
        var profile = new UserProfile
        {
            UserId = userId,
            Email = $"{name.ToLower().Replace(" ", "")}@example.com",
            Name = name,
            Gender = gender,
            DateOfBirth = dob,
            PhotoCount = photoCount,
            OnboardingStatus = status
        };
        await _context.UserProfiles.AddAsync(profile);
        await _context.SaveChangesAsync();

        if (addPreferences)
        {
            var prefs = new MatchPreferences
            {
                UserProfileId = profile.Id,
                UserId = userId,
                MinAge = 20,
                MaxAge = 35,
                MaxDistanceKm = 50,
                PreferredGender = "Any",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _context.MatchPreferences.AddAsync(prefs);
            await _context.SaveChangesAsync();
        }

        return profile;
    }

    // ======================== GET METRICS ========================

    [Fact]
    public async Task GetMetrics_EmptyDatabase_ReturnsAllZeros()
    {
        var result = await _controller.GetMetrics();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var metrics = Assert.IsType<OnboardingMetricsDto>(okResult.Value);
        Assert.Equal(0, metrics.TotalRegistered);
        Assert.Equal(0, metrics.CompletedBasicInfo);
        Assert.Equal(0, metrics.CompletedPhotos);
        Assert.Equal(0, metrics.CompletedPreferences);
        Assert.Equal(0, metrics.FullyOnboarded);
        Assert.Equal(0, metrics.ConversionRate);
    }

    [Fact]
    public async Task GetMetrics_FullyOnboardedUser_AllCountsCorrect()
    {
        await SeedProfile("Alice", "Female", new DateTime(1995, 1, 1),
            photoCount: 3, status: OnboardingStatus.Ready, addPreferences: true);

        var result = await _controller.GetMetrics();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var metrics = Assert.IsType<OnboardingMetricsDto>(okResult.Value);
        Assert.Equal(1, metrics.TotalRegistered);
        Assert.Equal(1, metrics.CompletedBasicInfo);
        Assert.Equal(1, metrics.CompletedPhotos);
        Assert.Equal(1, metrics.CompletedPreferences);
        Assert.Equal(1, metrics.FullyOnboarded);
        Assert.Equal(100.0, metrics.ConversionRate);
    }

    [Fact]
    public async Task GetMetrics_MixedCompletionLevels_CorrectCounts()
    {
        // User 1: fully onboarded
        await SeedProfile("Alice", "Female", new DateTime(1995, 1, 1),
            photoCount: 3, status: OnboardingStatus.Ready, addPreferences: true);

        // User 2: basic info only (no photos, no prefs, not ready)
        await SeedProfile("Bob", "Male", new DateTime(1990, 5, 10));

        // User 3: basic info + photos but not ready
        await SeedProfile("Charlie", "Male", new DateTime(1992, 3, 15),
            photoCount: 2);

        // User 4: incomplete profile (no name/gender/dob won't match since we always set them)
        // Instead: profile with empty name doesn't count as basic info
        var incompleteUserId = Guid.NewGuid();
        var incomplete = new UserProfile
        {
            UserId = incompleteUserId,
            Email = "incomplete@example.com",
            Name = "", // empty = not complete
            Gender = "",
            DateOfBirth = default
        };
        await _context.UserProfiles.AddAsync(incomplete);
        await _context.SaveChangesAsync();

        var result = await _controller.GetMetrics();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var metrics = Assert.IsType<OnboardingMetricsDto>(okResult.Value);
        Assert.Equal(4, metrics.TotalRegistered);
        Assert.Equal(3, metrics.CompletedBasicInfo); // Alice, Bob, Charlie have name+gender+dob
        Assert.Equal(2, metrics.CompletedPhotos);    // Alice (3) + Charlie (2)
        Assert.Equal(1, metrics.CompletedPreferences); // Alice only
        Assert.Equal(1, metrics.FullyOnboarded);     // Alice only
        Assert.Equal(25.0, metrics.ConversionRate);  // 1/4 = 25%
    }

    [Fact]
    public async Task GetMetrics_ConversionRateRoundsToOneDecimal()
    {
        // 1 ready out of 3 = 33.3%
        await SeedProfile("Alice", "Female", new DateTime(1995, 1, 1),
            status: OnboardingStatus.Ready);
        await SeedProfile("Bob", "Male", new DateTime(1990, 1, 1));
        await SeedProfile("Charlie", "Male", new DateTime(1992, 1, 1));

        var result = await _controller.GetMetrics();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var metrics = Assert.IsType<OnboardingMetricsDto>(okResult.Value);
        Assert.Equal(33.3, metrics.ConversionRate);
    }

    // ======================== GET STEP BREAKDOWN ========================

    [Fact]
    public async Task GetStepBreakdown_EmptyDatabase_ReturnsEmptyList()
    {
        var result = await _controller.GetStepBreakdown();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var steps = Assert.IsType<List<OnboardingStepMetric>>(okResult.Value);
        Assert.Empty(steps);
    }

    [Fact]
    public async Task GetStepBreakdown_WithUsers_ReturnsFourSteps()
    {
        await SeedProfile("Alice", "Female", new DateTime(1995, 1, 1),
            photoCount: 3, status: OnboardingStatus.Ready, addPreferences: true);
        await SeedProfile("Bob", "Male", new DateTime(1990, 1, 1));

        var result = await _controller.GetStepBreakdown();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var steps = Assert.IsType<List<OnboardingStepMetric>>(okResult.Value);
        Assert.Equal(4, steps.Count);
        Assert.Equal("BasicInfo", steps[0].StepName);
        Assert.Equal("Photos", steps[1].StepName);
        Assert.Equal("Preferences", steps[2].StepName);
        Assert.Equal("FullyOnboarded", steps[3].StepName);
    }

    [Fact]
    public async Task GetStepBreakdown_CorrectPercentages()
    {
        // 2 users, 1 has photos, 1 doesn't
        await SeedProfile("Alice", "Female", new DateTime(1995, 1, 1), photoCount: 3);
        await SeedProfile("Bob", "Male", new DateTime(1990, 1, 1), photoCount: 0);

        var result = await _controller.GetStepBreakdown();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var steps = Assert.IsType<List<OnboardingStepMetric>>(okResult.Value);

        var basicInfo = steps.First(s => s.StepName == "BasicInfo");
        Assert.Equal(2, basicInfo.CompletedCount);
        Assert.Equal(0, basicInfo.PendingCount);
        Assert.Equal(100.0, basicInfo.CompletionPercentage);

        var photos = steps.First(s => s.StepName == "Photos");
        Assert.Equal(1, photos.CompletedCount);
        Assert.Equal(1, photos.PendingCount);
        Assert.Equal(50.0, photos.CompletionPercentage);
    }

    [Fact]
    public async Task GetStepBreakdown_PendingCountsCorrect()
    {
        // 3 users, 0 ready
        await SeedProfile("Alice", "Female", new DateTime(1995, 1, 1));
        await SeedProfile("Bob", "Male", new DateTime(1990, 1, 1));
        await SeedProfile("Charlie", "Male", new DateTime(1992, 1, 1));

        var result = await _controller.GetStepBreakdown();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var steps = Assert.IsType<List<OnboardingStepMetric>>(okResult.Value);

        var fullyOnboarded = steps.First(s => s.StepName == "FullyOnboarded");
        Assert.Equal(0, fullyOnboarded.CompletedCount);
        Assert.Equal(3, fullyOnboarded.PendingCount);
        Assert.Equal(0, fullyOnboarded.CompletionPercentage);
    }
}
