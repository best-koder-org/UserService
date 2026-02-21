using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using UserService.Data;
using UserService.DTOs;
using UserService.Models;
using UserService.Services;

namespace UserService.Tests.Services;

public class VerificationServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IPhotoService> _photoServiceMock;
    private readonly VerificationService _service;

    public VerificationServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"Verification_{Guid.NewGuid()}")
            .Options;
        _context = new ApplicationDbContext(options);

        _photoServiceMock = new Mock<IPhotoService>();
        _photoServiceMock.Setup(p => p.ValidatePhotoAsync(It.IsAny<Microsoft.AspNetCore.Http.IFormFile>()))
            .ReturnsAsync(true);
        _photoServiceMock.Setup(p => p.UploadPhotoAsync(It.IsAny<int>(), It.IsAny<PhotoUploadDto>()))
            .ReturnsAsync(new PhotoResponseDto { PhotoUrl = "http://test/verify.jpg" });

        _service = new VerificationService(
            _context,
            _photoServiceMock.Object,
            Mock.Of<ILogger<VerificationService>>(),
            new ConfigurationBuilder().Build());
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
            Gender = "Male",
            DateOfBirth = new DateTime(1990, 1, 1),
            IsActive = true,
            IsPhoneVerified = false,
            IsEmailVerified = false,
            IsPhotoVerified = false,
            IsVerified = false
        };
        _context.UserProfiles.Add(user);
        _context.SaveChanges();
        return user;
    }

    // ===== Phone Verification =====

    [Fact]
    public async Task RequestPhoneVerification_ValidUser_ReturnsTrue()
    {
        var user = CreateTestUser();

        var result = await _service.RequestPhoneVerificationAsync(user.Id, "+1234567890");

        Assert.True(result);
    }

    [Fact]
    public async Task RequestPhoneVerification_NonExistentUser_ReturnsFalse()
    {
        var result = await _service.RequestPhoneVerificationAsync(999, "+1234567890");

        Assert.False(result);
    }

    [Fact]
    public async Task VerifyPhoneCode_CorrectCode_SetsIsPhoneVerified()
    {
        var user = CreateTestUser();
        await _service.RequestPhoneVerificationAsync(user.Id, "+1234567890");

        // We need to get the code — it's logged and stored in static dict
        // Use reflection to get the stored code
        var codesField = typeof(VerificationService).GetField("_phoneVerificationCodes",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var codes = codesField?.GetValue(null) as System.Collections.Generic.Dictionary<int, string>;
        var code = codes?[user.Id];

        Assert.NotNull(code);
        var result = await _service.VerifyPhoneCodeAsync(user.Id, code!);

        Assert.True(result);
        var updated = await _context.UserProfiles.FindAsync(user.Id);
        Assert.True(updated!.IsPhoneVerified);
    }

    [Fact]
    public async Task VerifyPhoneCode_WrongCode_ReturnsFalse()
    {
        var user = CreateTestUser();
        await _service.RequestPhoneVerificationAsync(user.Id, "+1234567890");

        var result = await _service.VerifyPhoneCodeAsync(user.Id, "000000");

        Assert.False(result);
    }

    [Fact]
    public async Task VerifyPhoneCode_NoCodeRequested_ReturnsFalse()
    {
        var user = CreateTestUser();

        var result = await _service.VerifyPhoneCodeAsync(user.Id, "123456");

        Assert.False(result);
    }

    [Fact]
    public async Task VerifyPhoneCode_NonExistentUser_ReturnsFalse()
    {
        var result = await _service.VerifyPhoneCodeAsync(999, "123456");

        Assert.False(result);
    }

    // ===== Email Verification =====

    [Fact]
    public async Task RequestEmailVerification_ValidUser_ReturnsTrue()
    {
        var user = CreateTestUser();

        var result = await _service.RequestEmailVerificationAsync(user.Id);

        Assert.True(result);
    }

    [Fact]
    public async Task RequestEmailVerification_NonExistentUser_ReturnsFalse()
    {
        var result = await _service.RequestEmailVerificationAsync(999);

        Assert.False(result);
    }

    [Fact]
    public async Task VerifyEmailToken_CorrectToken_SetsIsEmailVerified()
    {
        var user = CreateTestUser();
        await _service.RequestEmailVerificationAsync(user.Id);

        var tokensField = typeof(VerificationService).GetField("_emailVerificationTokens",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var tokens = tokensField?.GetValue(null) as System.Collections.Generic.Dictionary<int, string>;
        var token = tokens?[user.Id];

        Assert.NotNull(token);
        var result = await _service.VerifyEmailTokenAsync(user.Id, token!);

        Assert.True(result);
        var updated = await _context.UserProfiles.FindAsync(user.Id);
        Assert.True(updated!.IsEmailVerified);
    }

    [Fact]
    public async Task VerifyEmailToken_WrongToken_ReturnsFalse()
    {
        var user = CreateTestUser();
        await _service.RequestEmailVerificationAsync(user.Id);

        var result = await _service.VerifyEmailTokenAsync(user.Id, "wrong-token");

        Assert.False(result);
    }

    // ===== Photo Verification =====

    [Fact]
    public async Task ProcessPhotoVerification_Approved_SetsIsPhotoVerified()
    {
        var user = CreateTestUser();

        var result = await _service.ProcessPhotoVerificationAsync(user.Id, true);

        Assert.True(result);
        var updated = await _context.UserProfiles.FindAsync(user.Id);
        Assert.True(updated!.IsPhotoVerified);
    }

    [Fact]
    public async Task ProcessPhotoVerification_Rejected_DoesNotSetPhotoVerified()
    {
        var user = CreateTestUser();

        await _service.ProcessPhotoVerificationAsync(user.Id, false);

        var updated = await _context.UserProfiles.FindAsync(user.Id);
        Assert.False(updated!.IsPhotoVerified);
    }

    [Fact]
    public async Task ProcessPhotoVerification_NonExistentUser_ReturnsFalse()
    {
        var result = await _service.ProcessPhotoVerificationAsync(999, true);

        Assert.False(result);
    }

    // ===== Combined Verification (IsVerified) =====

    [Fact]
    public async Task ProcessPhotoVerification_AllVerified_SetsIsVerified()
    {
        var user = CreateTestUser();
        // Pre-set email and phone verified
        user.IsEmailVerified = true;
        user.IsPhoneVerified = true;
        _context.SaveChanges();

        await _service.ProcessPhotoVerificationAsync(user.Id, true);

        var updated = await _context.UserProfiles.FindAsync(user.Id);
        Assert.True(updated!.IsVerified);
        Assert.NotNull(updated.VerificationDate);
    }

    [Fact]
    public async Task ProcessPhotoVerification_PartiallyVerified_DoesNotSetIsVerified()
    {
        var user = CreateTestUser();
        user.IsEmailVerified = true;
        user.IsPhoneVerified = false; // Missing phone
        _context.SaveChanges();

        await _service.ProcessPhotoVerificationAsync(user.Id, true);

        var updated = await _context.UserProfiles.FindAsync(user.Id);
        Assert.False(updated!.IsVerified);
    }

    // ===== GetVerificationStatus =====

    [Fact]
    public async Task GetVerificationStatus_ReturnsCurrentState()
    {
        var user = CreateTestUser();
        user.IsEmailVerified = true;
        user.IsPhoneVerified = false;
        user.IsPhotoVerified = true;
        _context.SaveChanges();

        var status = await _service.GetVerificationStatusAsync(user.Id);

        Assert.True(status["email"]);
        Assert.False(status["phone"]);
        Assert.True(status["photo"]);
        Assert.False(status["overall"]);
    }

    [Fact]
    public async Task GetVerificationStatus_NonExistentUser_ReturnsEmpty()
    {
        var status = await _service.GetVerificationStatusAsync(999);

        Assert.Empty(status);
    }
}
