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
/// Tests for PreferencesController - managing dating match preferences
/// Critical user journey: Setting and retrieving match criteria
/// </summary>
public class PreferencesControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ILogger<PreferencesController>> _mockLogger;
    private readonly PreferencesController _controller;
    private readonly Guid _testUserId = Guid.NewGuid();
    private readonly string _testUserIdStr;
    private const string TestEmail = "bob@example.com";

    public PreferencesControllerTests()
    {
        _testUserIdStr = _testUserId.ToString();
        
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"PreferencesControllerTests_{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);
        _mockLogger = new Mock<ILogger<PreferencesController>>();
        _controller = new PreferencesController(_context, _mockLogger.Object);

        // Setup HTTP context with claims (simulating Keycloak JWT)
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, _testUserIdStr),
            new Claim("sub", _testUserIdStr),
            new Claim(ClaimTypes.Email, TestEmail)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task GetPreferences_NoPreferencesExist_ReturnsDefaults()
    {
        // Arrange - Create profile but no preferences
        var profile = new UserProfile
        {
            UserId = _testUserId,
            Email = TestEmail,
            Name = "Bob Smith",
            Gender = "Male",
            DateOfBirth = new DateTime(1990, 1, 1),
            OnboardingStatus = OnboardingStatus.Ready
        };
        await _context.UserProfiles.AddAsync(profile);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetPreferences(_testUserIdStr);

        // Assert - Should return default preferences
        var okResult = Assert.IsType<ActionResult<GetPreferencesResponse>>(result);
        var actionResult = Assert.IsType<OkObjectResult>(okResult.Result);
        var response = Assert.IsType<GetPreferencesResponse>(actionResult.Value);

        Assert.Equal(18, response.MinAge);
        Assert.Equal(35, response.MaxAge);
        Assert.Equal(50, response.MaxDistanceKm);
        Assert.Equal("Any", response.PreferredGender);
        Assert.Empty(response.RelationshipGoals);
        Assert.False(response.DealBreakerSmoking);
        Assert.True(response.ShowMeInDiscovery);
    }

    [Fact]
    public async Task GetPreferences_PreferencesExist_ReturnsStoredPreferences()
    {
        // Arrange
        var profile = new UserProfile
        {
            UserId = _testUserId,
            Email = TestEmail,
            Name = "Bob Smith",
            Gender = "Male",
            DateOfBirth = new DateTime(1990, 1, 1),
            OnboardingStatus = OnboardingStatus.Ready
        };
        await _context.UserProfiles.AddAsync(profile);
        await _context.SaveChangesAsync();

        var prefs = new MatchPreferences
        {
            UserProfileId = profile.Id,
            UserId = _testUserId,
            MinAge = 25,
            MaxAge = 40,
            MaxDistanceKm = 100,
            PreferredGender = "Female",
            RelationshipGoals = "[\"Long-term\",\"Marriage\"]",
            DealBreakerSmoking = true,
            DealBreakerDrinking = false,
            MinHeightCm = 160,
            MaxHeightCm = 180,
            ShowMeInDiscovery = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _context.Set<MatchPreferences>().AddAsync(prefs);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetPreferences(_testUserIdStr);

        // Assert
        var okResult = Assert.IsType<ActionResult<GetPreferencesResponse>>(result);
        var actionResult = Assert.IsType<OkObjectResult>(okResult.Result);
        var response = Assert.IsType<GetPreferencesResponse>(actionResult.Value);

        Assert.Equal(25, response.MinAge);
        Assert.Equal(40, response.MaxAge);
        Assert.Equal(100, response.MaxDistanceKm);
        Assert.Equal("Female", response.PreferredGender);
        Assert.Contains("Long-term", response.RelationshipGoals);
        Assert.Contains("Marriage", response.RelationshipGoals);
        Assert.True(response.DealBreakerSmoking);
        Assert.False(response.DealBreakerDrinking);
        Assert.Equal(160, response.MinHeightCm);
        Assert.Equal(180, response.MaxHeightCm);
    }

    [Fact]
    public async Task GetPreferences_DifferentUser_ReturnsForbidden()
    {
        // Arrange
        var otherUserId = Guid.NewGuid().ToString();
        var profile = new UserProfile
        {
            UserId = Guid.Parse(otherUserId),
            Email = "charlie@example.com",
            Name = "Charlie Brown",
            Gender = "Male",
            DateOfBirth = new DateTime(1992, 3, 10),
            OnboardingStatus = OnboardingStatus.Ready
        };
        await _context.UserProfiles.AddAsync(profile);
        await _context.SaveChangesAsync();

        // Act - Try to access another user's preferences
        var result = await _controller.GetPreferences(otherUserId);

        // Assert
        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task GetPreferences_ProfileNotFound_ReturnsNotFound()
    {
        // Act - Request preferences for non-existent user
        var result = await _controller.GetPreferences(_testUserIdStr);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Equal("User profile not found", notFoundResult.Value);
    }

    [Fact]
    public async Task UpdatePreferences_ValidData_CreatesNewPreferences()
    {
        // Arrange
        var profile = new UserProfile
        {
            UserId = _testUserId,
            Email = TestEmail,
            Name = "Bob Smith",
            Gender = "Male",
            DateOfBirth = new DateTime(1990, 1, 1),
            OnboardingStatus = OnboardingStatus.Ready
        };
        await _context.UserProfiles.AddAsync(profile);
        await _context.SaveChangesAsync();

        var request = new UpdatePreferencesRequest
        {
            MinAge = 22,
            MaxAge = 35,
            MaxDistanceKm = 75,
            PreferredGender = "Female",
            RelationshipGoals = new List<string> { "Long-term", "Casual" },
            DealBreakerSmoking = true,
            DealBreakerDrinking = false,
            ShowMeInDiscovery = true,
            MinHeightCm = 165,
            MaxHeightCm = 185
        };

        // Act
        var result = await _controller.UpdatePreferences(_testUserIdStr, request);

        // Assert
        var okResult = Assert.IsType<ActionResult<GetPreferencesResponse>>(result);
        var actionResult = Assert.IsType<OkObjectResult>(okResult.Result);
        var response = Assert.IsType<GetPreferencesResponse>(actionResult.Value);

        Assert.Equal(22, response.MinAge);
        Assert.Equal(35, response.MaxAge);
        Assert.Equal(75, response.MaxDistanceKm);
        Assert.Equal("Female", response.PreferredGender);
        Assert.True(response.DealBreakerSmoking);
        Assert.Equal(165, response.MinHeightCm);
        Assert.Equal(185, response.MaxHeightCm);

        // Verify database persistence
        var savedPrefs = await _context.Set<MatchPreferences>()
            .FirstOrDefaultAsync(p => p.UserProfileId == profile.Id);
        Assert.NotNull(savedPrefs);
        Assert.Equal(22, savedPrefs.MinAge);
    }

    [Fact]
    public async Task UpdatePreferences_InvalidAgeRange_ReturnsBadRequest()
    {
        // Arrange
        var profile = new UserProfile
        {
            UserId = _testUserId,
            Email = TestEmail,
            Name = "Bob Smith",
            Gender = "Male",
            DateOfBirth = new DateTime(1990, 1, 1),
            OnboardingStatus = OnboardingStatus.Ready
        };
        await _context.UserProfiles.AddAsync(profile);
        await _context.SaveChangesAsync();

        var request = new UpdatePreferencesRequest
        {
            MinAge = 40, // Invalid: Min > Max
            MaxAge = 25
        };

        // Act
        var result = await _controller.UpdatePreferences(_testUserIdStr, request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("MinAge cannot be greater than MaxAge", badRequestResult.Value!.ToString());
    }

    [Fact]
    public async Task UpdatePreferences_InvalidHeightRange_ReturnsBadRequest()
    {
        // Arrange
        var profile = new UserProfile
        {
            UserId = _testUserId,
            Email = TestEmail,
            Name = "Bob Smith",
            Gender = "Male",
            DateOfBirth = new DateTime(1990, 1, 1),
            OnboardingStatus = OnboardingStatus.Ready
        };
        await _context.UserProfiles.AddAsync(profile);
        await _context.SaveChangesAsync();

        var request = new UpdatePreferencesRequest
        {
            MinHeightCm = 190, // Invalid: Min > Max
            MaxHeightCm = 170
        };

        // Act
        var result = await _controller.UpdatePreferences(_testUserIdStr, request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("MinHeightCm cannot be greater than MaxHeightCm", badRequestResult.Value!.ToString());
    }

    [Fact]
    public async Task UpdatePreferences_ExistingPreferences_UpdatesValues()
    {
        // Arrange - Create profile and initial preferences
        var profile = new UserProfile
        {
            UserId = _testUserId,
            Email = TestEmail,
            Name = "Bob Smith",
            Gender = "Male",
            DateOfBirth = new DateTime(1990, 1, 1),
            OnboardingStatus = OnboardingStatus.Ready
        };
        await _context.UserProfiles.AddAsync(profile);
        await _context.SaveChangesAsync();

        var initialPrefs = new MatchPreferences
        {
            UserProfileId = profile.Id,
            UserId = _testUserId,
            MinAge = 20,
            MaxAge = 30,
            MaxDistanceKm = 50,
            PreferredGender = "Any",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _context.Set<MatchPreferences>().AddAsync(initialPrefs);
        await _context.SaveChangesAsync();

        var updateRequest = new UpdatePreferencesRequest
        {
            MinAge = 25,
            MaxAge = 40,
            PreferredGender = "Female"
        };

        // Act
        var result = await _controller.UpdatePreferences(_testUserIdStr, updateRequest);

        // Assert
        var okResult = Assert.IsType<ActionResult<GetPreferencesResponse>>(result);
        var actionResult = Assert.IsType<OkObjectResult>(okResult.Result);
        var response = Assert.IsType<GetPreferencesResponse>(actionResult.Value);

        Assert.Equal(25, response.MinAge);
        Assert.Equal(40, response.MaxAge);
        Assert.Equal("Female", response.PreferredGender);

        // Verify only one preference record exists (updated, not duplicated)
        var prefCount = await _context.Set<MatchPreferences>()
            .CountAsync(p => p.UserProfileId == profile.Id);
        Assert.Equal(1, prefCount);
    }

    [Fact]
    public async Task UpdatePreferences_DifferentUser_ReturnsForbidden()
    {
        // Arrange
        var otherUserId = Guid.NewGuid().ToString();
        var request = new UpdatePreferencesRequest { MinAge = 25, MaxAge = 35 };

        // Act - Try to update another user's preferences
        var result = await _controller.UpdatePreferences(otherUserId, request);

        // Assert
        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task UpdatePreferences_ProfileNotFound_ReturnsNotFound()
    {
        // Arrange
        var request = new UpdatePreferencesRequest { MinAge = 25, MaxAge = 35 };

        // Act - Try to update preferences for non-existent profile
        var result = await _controller.UpdatePreferences(_testUserIdStr, request);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Equal("User profile not found", notFoundResult.Value);
    }
}
