using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using UserService.Controllers;
using UserService.DTOs;

namespace UserService.Tests.Controllers;

/// <summary>
/// Tests for DemoController — generates static demo profiles for testing.
/// No auth required, no DB access, purely deterministic generation.
/// </summary>
public class DemoControllerTests
{
    private readonly Mock<ILogger<DemoController>> _mockLogger;
    private readonly DemoController _controller;

    public DemoControllerTests()
    {
        _mockLogger = new Mock<ILogger<DemoController>>();
        _controller = new DemoController(_mockLogger.Object);
    }

    // ======================== GET DEMO PROFILES ========================

    [Fact]
    public void GetDemoProfiles_DefaultCount_ReturnsTenProfiles()
    {
        var result = _controller.GetDemoProfiles();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var profiles = Assert.IsType<List<UserProfileSummaryDto>>(okResult.Value);
        Assert.Equal(10, profiles.Count);
    }

    [Fact]
    public void GetDemoProfiles_CustomCount_ReturnsRequestedAmount()
    {
        var result = _controller.GetDemoProfiles(count: 5);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var profiles = Assert.IsType<List<UserProfileSummaryDto>>(okResult.Value);
        Assert.Equal(5, profiles.Count);
    }

    [Fact]
    public void GetDemoProfiles_HasRequiredFields()
    {
        var result = _controller.GetDemoProfiles(count: 1);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var profiles = Assert.IsType<List<UserProfileSummaryDto>>(okResult.Value);
        var profile = profiles.First();

        Assert.Equal(1, profile.Id);
        Assert.False(string.IsNullOrEmpty(profile.Name));
        Assert.True(profile.Age >= 22 && profile.Age <= 36);
        Assert.False(string.IsNullOrEmpty(profile.City));
        Assert.False(string.IsNullOrEmpty(profile.PrimaryPhotoUrl));
        Assert.NotNull(profile.PhotoUrls);
        Assert.True(profile.PhotoUrls.Count >= 3);
        Assert.False(string.IsNullOrEmpty(profile.Bio));
        Assert.False(string.IsNullOrEmpty(profile.Occupation));
        Assert.NotNull(profile.Interests);
        Assert.NotEmpty(profile.Interests);
        Assert.NotNull(profile.Prompts);
        Assert.NotEmpty(profile.Prompts);
    }

    [Fact]
    public void GetDemoProfiles_ProfilesHaveUniqueIds()
    {
        var result = _controller.GetDemoProfiles(count: 20);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var profiles = Assert.IsType<List<UserProfileSummaryDto>>(okResult.Value);
        var ids = profiles.Select(p => p.Id).ToList();
        Assert.Equal(ids.Distinct().Count(), ids.Count);
    }

    [Fact]
    public void GetDemoProfiles_VoicePromptUrlPattern()
    {
        // Every 3rd profile (i % 3 == 0) should have a voice prompt URL
        var result = _controller.GetDemoProfiles(count: 6);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var profiles = Assert.IsType<List<UserProfileSummaryDto>>(okResult.Value);

        Assert.NotNull(profiles[0].VoicePromptUrl); // i=0: 0%3==0
        Assert.Null(profiles[1].VoicePromptUrl);    // i=1
        Assert.Null(profiles[2].VoicePromptUrl);    // i=2
        Assert.NotNull(profiles[3].VoicePromptUrl); // i=3: 3%3==0
    }

    // ======================== GET DEMO PROFILE BY ID ========================

    [Fact]
    public void GetDemoProfile_ValidId_ReturnsDetailedProfile()
    {
        var result = _controller.GetDemoProfile(42);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var profile = Assert.IsType<UserProfileDetailDto>(okResult.Value);
        Assert.Equal(42, profile.Id);
        Assert.False(string.IsNullOrEmpty(profile.Name));
        Assert.False(string.IsNullOrEmpty(profile.Email));
        Assert.Contains("@example.com", profile.Email);
        Assert.NotNull(profile.PhotoUrls);
        Assert.True(profile.PhotoUrls.Count >= 3);
        Assert.False(string.IsNullOrEmpty(profile.City));
        Assert.Equal("Sweden", profile.Country);
    }

    [Fact]
    public void GetDemoProfile_EvenId_ReturnsFemale()
    {
        var result = _controller.GetDemoProfile(2);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var profile = Assert.IsType<UserProfileDetailDto>(okResult.Value);
        Assert.Equal("Female", profile.Gender);
    }

    [Fact]
    public void GetDemoProfile_OddId_ReturnsMale()
    {
        var result = _controller.GetDemoProfile(3);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var profile = Assert.IsType<UserProfileDetailDto>(okResult.Value);
        Assert.Equal("Male", profile.Gender);
    }

    [Fact]
    public void GetDemoProfile_PremiumUser_IdDivisibleByFive()
    {
        var result = _controller.GetDemoProfile(5);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var profile = Assert.IsType<UserProfileDetailDto>(okResult.Value);
        Assert.True(profile.IsPremium);
        Assert.Equal("Premium", profile.SubscriptionType);
    }

    [Fact]
    public void GetDemoProfile_NonPremiumUser()
    {
        var result = _controller.GetDemoProfile(3);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var profile = Assert.IsType<UserProfileDetailDto>(okResult.Value);
        Assert.False(profile.IsPremium);
        Assert.Equal("Free", profile.SubscriptionType);
    }

    // ======================== SEARCH DEMO PROFILES ========================

    [Fact]
    public void SearchDemoProfiles_NoFilters_ReturnsPage()
    {
        var searchDto = new SearchUsersDto { Page = 1, PageSize = 10 };

        var result = _controller.SearchDemoProfiles(searchDto);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var searchResult = Assert.IsType<SearchResultDto<UserProfileSummaryDto>>(okResult.Value);
        Assert.True(searchResult.TotalCount > 0);
        Assert.Equal(1, searchResult.Page);
        Assert.Equal(10, searchResult.PageSize);
        Assert.True(searchResult.Results.Count <= 10);
    }

    [Fact]
    public void SearchDemoProfiles_MinAgeFilter_FiltersResults()
    {
        var searchDto = new SearchUsersDto { MinAge = 30, Page = 1, PageSize = 50 };

        var result = _controller.SearchDemoProfiles(searchDto);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var searchResult = Assert.IsType<SearchResultDto<UserProfileSummaryDto>>(okResult.Value);

        Assert.All(searchResult.Results, p => Assert.True(p.Age >= 30));
    }

    [Fact]
    public void SearchDemoProfiles_MaxAgeFilter_FiltersResults()
    {
        var searchDto = new SearchUsersDto { MaxAge = 25, Page = 1, PageSize = 50 };

        var result = _controller.SearchDemoProfiles(searchDto);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var searchResult = Assert.IsType<SearchResultDto<UserProfileSummaryDto>>(okResult.Value);

        Assert.All(searchResult.Results, p => Assert.True(p.Age <= 25));
    }

    [Fact]
    public void SearchDemoProfiles_Pagination_HasNextAndPrevious()
    {
        var pageOneDto = new SearchUsersDto { Page = 1, PageSize = 5 };

        var result = _controller.SearchDemoProfiles(pageOneDto);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var searchResult = Assert.IsType<SearchResultDto<UserProfileSummaryDto>>(okResult.Value);

        if (searchResult.TotalPages > 1)
        {
            Assert.True(searchResult.HasNext);
            Assert.False(searchResult.HasPrevious);
        }
    }

    [Fact]
    public void SearchDemoProfiles_SecondPage_HasPrevious()
    {
        var pageTwoDto = new SearchUsersDto { Page = 2, PageSize = 5 };

        var result = _controller.SearchDemoProfiles(pageTwoDto);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var searchResult = Assert.IsType<SearchResultDto<UserProfileSummaryDto>>(okResult.Value);

        Assert.True(searchResult.HasPrevious);
        Assert.Equal(2, searchResult.Page);
    }

    // ======================== HEALTH CHECK ========================

    [Fact]
    public void DemoHealthCheck_ReturnsOk()
    {
        var result = _controller.DemoHealthCheck();

        Assert.IsType<OkObjectResult>(result);
    }
}
