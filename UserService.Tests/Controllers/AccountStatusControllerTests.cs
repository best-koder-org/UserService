using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using UserService.Common;
using UserService.Controllers;
using UserService.Data;
using UserService.DTOs;
using UserService.Models;
using Xunit;

namespace UserService.Tests.Controllers;

public class AccountStatusControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly AccountStatusController _controller;
    private readonly Guid _testUserId = Guid.NewGuid();

    public AccountStatusControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        var logger = new Mock<ILogger<AccountStatusController>>();
        _controller = new AccountStatusController(_context, logger.Object);

        // Set up authenticated user
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, _testUserId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        // Seed test profile
        _context.UserProfiles.Add(new UserProfile
        {
            UserId = _testUserId,
            Name = "Test User",
            Email = "test@example.com",
            DateOfBirth = new DateTime(1990, 1, 1),
            AccountStatus = "Active",
            IsActive = true
        });
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task PauseAccount_ActiveUser_ReturnsPaused()
    {
        var request = new AccountPauseRequest(PauseDuration.Hours24, "Need a break");

        var result = await _controller.PauseAccount(request) as OkObjectResult;

        Assert.NotNull(result);
        var response = result.Value as ApiResponse<AccountStatusResponse>;
        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.Equal(AccountStatus.Paused, response.Data!.Status);
        Assert.NotNull(response.Data.PausedAt);
        Assert.NotNull(response.Data.ResumeAt);
        Assert.Equal("Need a break", response.Data.PauseReason);
    }

    [Fact]
    public async Task PauseAccount_IndefiniteDuration_NoResumeAt()
    {
        var request = new AccountPauseRequest(PauseDuration.Indefinite);

        var result = await _controller.PauseAccount(request) as OkObjectResult;

        Assert.NotNull(result);
        var response = result.Value as ApiResponse<AccountStatusResponse>;
        Assert.Null(response!.Data!.ResumeAt);
    }

    [Fact]
    public async Task PauseAccount_AlreadyPaused_ReturnsBadRequest()
    {
        var profile = await _context.UserProfiles.FirstAsync(p => p.UserId == _testUserId);
        profile.AccountStatus = "Paused";
        await _context.SaveChangesAsync();

        var request = new AccountPauseRequest(PauseDuration.Hours24);

        var result = await _controller.PauseAccount(request);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task PauseAccount_SetsIsActiveFalse()
    {
        var request = new AccountPauseRequest(PauseDuration.OneWeek);

        await _controller.PauseAccount(request);

        var profile = await _context.UserProfiles.FirstAsync(p => p.UserId == _testUserId);
        Assert.False(profile.IsActive);
        Assert.Equal("Paused", profile.AccountStatus);
    }

    [Fact]
    public async Task ResumeAccount_PausedUser_ReturnsActive()
    {
        // First pause
        await _controller.PauseAccount(new AccountPauseRequest(PauseDuration.Hours24));

        // Then resume
        var result = await _controller.ResumeAccount() as OkObjectResult;

        Assert.NotNull(result);
        var response = result.Value as ApiResponse<AccountStatusResponse>;
        Assert.Equal(AccountStatus.Active, response!.Data!.Status);
        Assert.Null(response.Data.PausedAt);
        Assert.Null(response.Data.ResumeAt);
    }

    [Fact]
    public async Task ResumeAccount_NotPaused_ReturnsBadRequest()
    {
        var result = await _controller.ResumeAccount();

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ResumeAccount_SetsIsActiveTrue()
    {
        await _controller.PauseAccount(new AccountPauseRequest(PauseDuration.Hours72));
        await _controller.ResumeAccount();

        var profile = await _context.UserProfiles.FirstAsync(p => p.UserId == _testUserId);
        Assert.True(profile.IsActive);
        Assert.Equal("Active", profile.AccountStatus);
    }

    [Fact]
    public async Task GetStatus_ActiveUser_ReturnsActive()
    {
        var result = await _controller.GetAccountStatus() as OkObjectResult;

        Assert.NotNull(result);
        var response = result.Value as ApiResponse<AccountStatusResponse>;
        Assert.Equal(AccountStatus.Active, response!.Data!.Status);
    }

    [Fact]
    public async Task GetStatus_ExpiredPause_AutoResumes()
    {
        var profile = await _context.UserProfiles.FirstAsync(p => p.UserId == _testUserId);
        profile.AccountStatus = "Paused";
        profile.PausedAt = DateTime.UtcNow.AddDays(-2);
        profile.PauseUntil = DateTime.UtcNow.AddHours(-1); // expired
        profile.IsActive = false;
        await _context.SaveChangesAsync();

        var result = await _controller.GetAccountStatus() as OkObjectResult;

        Assert.NotNull(result);
        var response = result.Value as ApiResponse<AccountStatusResponse>;
        Assert.Equal(AccountStatus.Active, response!.Data!.Status);

        // Verify DB was updated
        var dbProfile = await _context.UserProfiles.FirstAsync(p => p.UserId == _testUserId);
        Assert.True(dbProfile.IsActive);
    }

    [Fact]
    public async Task GetStatus_NoProfile_ReturnsNotFound()
    {
        // Create controller with different user
        var logger = new Mock<ILogger<AccountStatusController>>();
        var ctrl = new AccountStatusController(_context, logger.Object);
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()) };
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) }
        };

        var result = await ctrl.GetAccountStatus();

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Theory]
    [InlineData(PauseDuration.Hours24)]
    [InlineData(PauseDuration.Hours72)]
    [InlineData(PauseDuration.OneWeek)]
    public async Task PauseAccount_TimedDurations_SetCorrectResumeAt(PauseDuration duration)
    {
        var before = DateTime.UtcNow;
        var request = new AccountPauseRequest(duration);

        var result = await _controller.PauseAccount(request) as OkObjectResult;
        var response = result!.Value as ApiResponse<AccountStatusResponse>;

        Assert.NotNull(response!.Data!.ResumeAt);
        var resumeAt = response.Data.ResumeAt!.Value;

        var expectedHours = duration switch
        {
            PauseDuration.Hours24 => 24,
            PauseDuration.Hours72 => 72,
            PauseDuration.OneWeek => 168,
            _ => 0
        };

        Assert.InRange(resumeAt, before.AddHours(expectedHours - 1), before.AddHours(expectedHours + 1));
    }
}
