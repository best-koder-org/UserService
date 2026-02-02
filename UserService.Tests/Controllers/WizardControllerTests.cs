using Xunit;
using Moq;
using MediatR;
using UserService.Controllers;
using UserService.Commands;
using UserService.Common;
using UserService.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace UserService.Tests.Controllers;

public class WizardControllerTests
{
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<ILogger<WizardController>> _mockLogger;
    private readonly WizardController _controller;
    private const string TestUserId = "12345678-1234-1234-1234-123456789012";
    private const string TestEmail = "test@example.com";

    public WizardControllerTests()
    {
        _mockMediator = new Mock<IMediator>();
        _mockLogger = new Mock<ILogger<WizardController>>();
        _controller = new WizardController(_mockMediator.Object, _mockLogger.Object);

        // Setup HTTP context with claims
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, TestUserId),
            new Claim("sub", TestUserId),
            new Claim(ClaimTypes.Email, TestEmail),
            new Claim("email", TestEmail)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    [Fact]
    public async Task UpdateStepBasicInfo_ValidData_ReturnsOk()
    {
        // Arrange
        var dto = new WizardStepBasicInfoDto
        {
            FirstName = "Alice",
            LastName = "Johnson",
            DateOfBirth = new DateTime(1995, 5, 15),
            Gender = "Female"
        };

        var expectedProfile = new UserProfileDetailDto
        {
            Id = 1,
            Name = "Alice Johnson",
            Gender = "Female",
            Age = 28,
            OnboardingStatus = Models.OnboardingStatus.Incomplete
        };

        _mockMediator
            .Setup(m => m.Send(It.IsAny<UpdateWizardStepCommand>(), default))
            .ReturnsAsync(Result<UserProfileDetailDto>.Success(expectedProfile));

        // Act
        var result = await _controller.UpdateStepBasicInfo(dto);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<UserProfileDetailDto>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal("Alice Johnson", response.Data!.Name);
        Assert.Equal("Female", response.Data.Gender);

        _mockMediator.Verify(m => m.Send(It.Is<UpdateWizardStepCommand>(cmd =>
            cmd.Step == 1 &&
            cmd.BasicInfo!.FirstName == "Alice" &&
            cmd.BasicInfo.LastName == "Johnson"
        ), default), Times.Once);
    }
    
    [Fact]
    public async Task UpdateStepPreferences_ValidData_ReturnsOk()
    {
        // Arrange
        var dto = new WizardStepPreferencesDto
        {
            PreferredGender = "Male",
            MinAge = 25,
            MaxAge = 35,
            MaxDistance = 50,
            Bio = "Love hiking and photography!"
        };

        var expectedProfile = new UserProfileDetailDto
        {
            Id = 1,
            Name = "Alice Johnson",
            Bio = "Love hiking and photography!",
            Preferences = "Male",
            OnboardingStatus = Models.OnboardingStatus.Incomplete
        };

        _mockMediator
            .Setup(m => m.Send(It.IsAny<UpdateWizardStepCommand>(), default))
            .ReturnsAsync(Result<UserProfileDetailDto>.Success(expectedProfile));

        // Act
        var result = await _controller.UpdateStepPreferences(dto);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<UserProfileDetailDto>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal("Love hiking and photography!", response.Data!.Bio);
        Assert.Equal("Male", response.Data.Preferences);

        _mockMediator.Verify(m => m.Send(It.Is<UpdateWizardStepCommand>(cmd =>
            cmd.Step == 2 &&
            cmd.Preferences!.PreferredGender == "Male" &&
            cmd.Preferences.MinAge == 25 &&
            cmd.Preferences.MaxAge == 35
        ), default), Times.Once);
    }
    
    [Fact]
    public async Task CompleteWizard_WithPhotos_MarksProfileReady()
    {
        // Arrange
        var dto = new WizardStepPhotosDto
        {
            PhotoUrls = new List<string>
            {
                "https://photos.example.com/alice-1.jpg",
                "https://photos.example.com/alice-2.jpg"
            }
        };

        var expectedProfile = new UserProfileDetailDto
        {
            Id = 1,
            Name = "Alice Johnson",
            PhotoUrls = dto.PhotoUrls,
            PrimaryPhotoUrl = dto.PhotoUrls[0],
            OnboardingStatus = Models.OnboardingStatus.Ready,
            OnboardingCompletedAt = DateTime.UtcNow
        };

        _mockMediator
            .Setup(m => m.Send(It.IsAny<UpdateWizardStepCommand>(), default))
            .ReturnsAsync(Result<UserProfileDetailDto>.Success(expectedProfile));

        // Act
        var result = await _controller.CompleteWizard(dto);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<UserProfileDetailDto>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal(Models.OnboardingStatus.Ready, response.Data!.OnboardingStatus);
        Assert.NotNull(response.Data.OnboardingCompletedAt);
        Assert.Equal(2, response.Data.PhotoUrls.Count);
        Assert.Equal(dto.PhotoUrls[0], response.Data.PrimaryPhotoUrl);

        _mockMediator.Verify(m => m.Send(It.Is<UpdateWizardStepCommand>(cmd =>
            cmd.Step == 3 &&
            cmd.Photos!.PhotoUrls.Count == 2
        ), default), Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("completed onboarding wizard")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()!),
            Times.Once);
    }

    [Fact]
    public async Task UpdateStepBasicInfo_HandlerReturnsFailure_ReturnsBadRequest()
    {
        // Arrange
        var dto = new WizardStepBasicInfoDto
        {
            FirstName = "Alice",
            LastName = "Johnson",
            DateOfBirth = new DateTime(2010, 5, 15), // Too young
            Gender = "Female"
        };

        _mockMediator
            .Setup(m => m.Send(It.IsAny<UpdateWizardStepCommand>(), default))
            .ReturnsAsync(Result<UserProfileDetailDto>.Failure("Invalid basic info - check age requirement (18+)"));

        // Act
        var result = await _controller.UpdateStepBasicInfo(dto);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<ApiResponse<UserProfileDetailDto>>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Contains("age requirement", response.Message);
    }

    [Fact]
    public async Task UpdateStepPreferences_HandlerReturnsFailure_ReturnsBadRequest()
    {
        // Arrange
        var dto = new WizardStepPreferencesDto
        {
            PreferredGender = "Male",
            MinAge = 35,
            MaxAge = 25, // Invalid: min > max
            MaxDistance = 50
        };

        _mockMediator
            .Setup(m => m.Send(It.IsAny<UpdateWizardStepCommand>(), default))
            .ReturnsAsync(Result<UserProfileDetailDto>.Failure("Invalid preferences - check age/distance settings"));

        // Act
        var result = await _controller.UpdateStepPreferences(dto);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<ApiResponse<UserProfileDetailDto>>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Contains("preferences", response.Message);
    }

    [Fact]
    public async Task CompleteWizard_InsufficientPhotos_ReturnsBadRequest()
    {
        // Arrange
        var dto = new WizardStepPhotosDto
        {
            PhotoUrls = new List<string>() // No photos
        };

        _mockMediator
            .Setup(m => m.Send(It.IsAny<UpdateWizardStepCommand>(), default))
            .ReturnsAsync(Result<UserProfileDetailDto>.Failure("At least 1 photo required to complete wizard"));

        // Act
        var result = await _controller.CompleteWizard(dto);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<ApiResponse<UserProfileDetailDto>>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Contains("photo required", response.Message);
    }
}
