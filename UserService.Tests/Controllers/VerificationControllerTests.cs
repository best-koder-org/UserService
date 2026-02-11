using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using UserService.Controllers;
using UserService.Common;
using UserService.Data;
using UserService.DTOs;
using UserService.Models;

namespace UserService.Tests.Controllers;

/// <summary>
/// Tests for VerificationController — user verification status and verification requests.
/// Uses real InMemory ApplicationDbContext (no mocking).
/// </summary>
public class VerificationControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ILogger<VerificationController>> _mockLogger;
    private readonly VerificationController _controller;
    private readonly Guid _testUserId = Guid.NewGuid();
    private readonly string _testUserIdStr;

    public VerificationControllerTests()
    {
        _testUserIdStr = _testUserId.ToString();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"VerificationTests_{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);
        _mockLogger = new Mock<ILogger<VerificationController>>();
        _controller = new VerificationController(_context, _mockLogger.Object);

        SetupAuth(_testUserIdStr);
    }

    private void SetupAuth(string userId)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim("sub", userId)
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

    // ======================== GET VERIFICATION STATUS ========================

    [Fact]
    public async Task GetStatus_UserWithPhotos_ReturnsPhotoVerified()
    {
        var profile = new UserProfile
        {
            UserId = _testUserId,
            Email = "alice@example.com",
            Name = "Alice",
            Gender = "Female",
            DateOfBirth = new DateTime(1995, 1, 1),
            PhotoUrls = "[\"https://example.com/photo1.jpg\"]"
        };
        await _context.UserProfiles.AddAsync(profile);
        await _context.SaveChangesAsync();

        var result = await _controller.GetVerificationStatus(_testUserIdStr);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<VerificationStatusDto>>(okResult.Value);
        Assert.True(response.Success);
        Assert.True(response.Data!.PhotoVerified);
        Assert.True(response.Data.EmailVerified);
        Assert.False(response.Data.PhoneVerified);
        Assert.Equal(VerificationLevel.Basic, response.Data.OverallLevel);
        Assert.Contains("photo verification", response.Data.VerificationNote!);
    }

    [Fact]
    public async Task GetStatus_UserWithoutPhotos_ReturnsNotPhotoVerified()
    {
        var profile = new UserProfile
        {
            UserId = _testUserId,
            Email = "bob@example.com",
            Name = "Bob",
            Gender = "Male",
            DateOfBirth = new DateTime(1990, 1, 1),
            PhotoUrls = ""
        };
        await _context.UserProfiles.AddAsync(profile);
        await _context.SaveChangesAsync();

        var result = await _controller.GetVerificationStatus(_testUserIdStr);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<VerificationStatusDto>>(okResult.Value);
        Assert.True(response.Success);
        Assert.False(response.Data!.PhotoVerified);
        Assert.True(response.Data.EmailVerified);
        Assert.Equal(VerificationLevel.Basic, response.Data.OverallLevel);
        Assert.Contains("Upload a photo", response.Data.VerificationNote!);
    }

    [Fact]
    public async Task GetStatus_InvalidUserIdFormat_ReturnsBadRequest()
    {
        var result = await _controller.GetVerificationStatus("not-a-guid");

        var badResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<VerificationStatusDto>>(badResult.Value);
        Assert.False(response.Success);
        Assert.Equal("INVALID_USER_ID", response.ErrorCode);
    }

    [Fact]
    public async Task GetStatus_ProfileNotFound_ReturnsNotFound()
    {
        var nonExistentId = Guid.NewGuid().ToString();

        var result = await _controller.GetVerificationStatus(nonExistentId);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<VerificationStatusDto>>(notFoundResult.Value);
        Assert.False(response.Success);
        Assert.Equal("PROFILE_NOT_FOUND", response.ErrorCode);
    }
    // ======================== REQUEST VERIFICATION ========================

    [Fact]
    public async Task RequestVerification_PhotoType_ReturnsPhotoInstructions()
    {
        var request = new VerificationRequestDto { VerificationType = "photo" };

        var result = await _controller.RequestVerification(_testUserIdStr, request);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<VerificationRequestResponseDto>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal("photo", response.Data!.VerificationType);
        Assert.Equal("Pending", response.Data.Status);
        Assert.Contains("selfie", response.Data.Instructions!);
    }

    [Fact]
    public async Task RequestVerification_EmailType_ReturnsEmailInstructions()
    {
        var request = new VerificationRequestDto { VerificationType = "email" };

        var result = await _controller.RequestVerification(_testUserIdStr, request);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<VerificationRequestResponseDto>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal("email", response.Data!.VerificationType);
        Assert.Contains("email", response.Data.Instructions!.ToLower());
    }

    [Fact]
    public async Task RequestVerification_PhoneType_ReturnsNotAvailable()
    {
        var request = new VerificationRequestDto { VerificationType = "phone" };

        var result = await _controller.RequestVerification(_testUserIdStr, request);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<VerificationRequestResponseDto>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal("phone", response.Data!.VerificationType);
        Assert.Contains("not yet available", response.Data.Instructions!);
    }

    [Fact]
    public async Task RequestVerification_InvalidType_ReturnsBadRequest()
    {
        var request = new VerificationRequestDto { VerificationType = "fingerprint" };

        var result = await _controller.RequestVerification(_testUserIdStr, request);

        var badResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<VerificationRequestResponseDto>>(badResult.Value);
        Assert.False(response.Success);
        Assert.Equal("INVALID_VERIFICATION_TYPE", response.ErrorCode);
    }

    [Fact]
    public async Task RequestVerification_DifferentUser_ReturnsForbid()
    {
        var differentUserId = Guid.NewGuid().ToString();
        var request = new VerificationRequestDto { VerificationType = "photo" };

        var result = await _controller.RequestVerification(differentUserId, request);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task RequestVerification_CaseInsensitiveType_Works()
    {
        var request = new VerificationRequestDto { VerificationType = "PHOTO" };

        var result = await _controller.RequestVerification(_testUserIdStr, request);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<VerificationRequestResponseDto>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal("photo", response.Data!.VerificationType);
    }
}
