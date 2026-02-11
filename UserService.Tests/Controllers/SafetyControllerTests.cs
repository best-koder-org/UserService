using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using UserService.Controllers;
using UserService.Common;
using UserService.DTOs;
using UserService.Models;
using UserService.Services;

namespace UserService.Tests.Controllers;

/// <summary>
/// Tests for SafetyController — blocking users, submitting reports, checking block status.
/// SafetyService is mocked (in-memory implementation in production, but we mock the interface).
/// </summary>
public class SafetyControllerTests
{
    private readonly Mock<ISafetyService> _mockSafetyService;
    private readonly Mock<ILogger<SafetyController>> _mockLogger;
    private readonly SafetyController _controller;
    private const string TestUserId = "user-abc-123";

    public SafetyControllerTests()
    {
        _mockSafetyService = new Mock<ISafetyService>();
        _mockLogger = new Mock<ILogger<SafetyController>>();
        _controller = new SafetyController(_mockSafetyService.Object, _mockLogger.Object);

        SetupAuth(TestUserId);
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

    private void SetupNoAuth()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    // ======================== BLOCK USER TESTS ========================

    [Fact]
    public async Task BlockUser_ValidRequest_ReturnsOkWithBlockResponse()
    {
        var request = new BlockUserDto { TargetUserId = "user-def-456" };
        _mockSafetyService.Setup(s => s.BlockUserAsync(TestUserId, "user-def-456"))
            .ReturnsAsync(true);

        var result = await _controller.BlockUser(request);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<BlockResponseDto>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal(TestUserId, response.Data!.BlockerId);
        Assert.Equal("user-def-456", response.Data.BlockedUserId);
    }

    [Fact]
    public async Task BlockUser_SelfBlock_ReturnsBadRequest()
    {
        var request = new BlockUserDto { TargetUserId = TestUserId };

        var result = await _controller.BlockUser(request);

        var badResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<BlockResponseDto>>(badResult.Value);
        Assert.False(response.Success);
        Assert.Equal("SELF_BLOCK", response.ErrorCode);
    }

    [Fact]
    public async Task BlockUser_NoAuth_ReturnsUnauthorized()
    {
        SetupNoAuth();
        var request = new BlockUserDto { TargetUserId = "anyone" };

        var result = await _controller.BlockUser(request);

        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<BlockResponseDto>>(unauthorizedResult.Value);
        Assert.Equal("INVALID_TOKEN", response.ErrorCode);
    }

    // ======================== UNBLOCK USER TESTS ========================

    [Fact]
    public async Task UnblockUser_ValidRequest_ReturnsOkWithResponse()
    {
        var request = new BlockUserDto { TargetUserId = "user-def-456" };
        _mockSafetyService.Setup(s => s.UnblockUserAsync(TestUserId, "user-def-456"))
            .ReturnsAsync(true);

        var result = await _controller.UnblockUser(request);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<BlockResponseDto>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal(TestUserId, response.Data!.BlockerId);
        Assert.Equal("user-def-456", response.Data.BlockedUserId);
    }

    [Fact]
    public async Task UnblockUser_NoAuth_ReturnsUnauthorized()
    {
        SetupNoAuth();
        var request = new BlockUserDto { TargetUserId = "anyone" };

        var result = await _controller.UnblockUser(request);

        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<BlockResponseDto>>(unauthorizedResult.Value);
        Assert.Equal("INVALID_TOKEN", response.ErrorCode);
    }

    // ======================== IS-BLOCKED TESTS ========================

    [Fact]
    public async Task IsBlocked_UserIsBlocked_ReturnsTrue()
    {
        _mockSafetyService.Setup(s => s.IsBlockedAsync(TestUserId, "blocked-user"))
            .ReturnsAsync(true);

        var result = await _controller.IsBlocked("blocked-user");

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<IsBlockedResponseDto>>(okResult.Value);
        Assert.True(response.Success);
        Assert.True(response.Data!.IsBlocked);
    }

    [Fact]
    public async Task IsBlocked_UserIsNotBlocked_ReturnsFalse()
    {
        _mockSafetyService.Setup(s => s.IsBlockedAsync(TestUserId, "friend-user"))
            .ReturnsAsync(false);

        var result = await _controller.IsBlocked("friend-user");

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<IsBlockedResponseDto>>(okResult.Value);
        Assert.True(response.Success);
        Assert.False(response.Data!.IsBlocked);
    }

    [Fact]
    public async Task IsBlocked_NoAuth_ReturnsUnauthorized()
    {
        SetupNoAuth();

        var result = await _controller.IsBlocked("anyone");

        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.IsType<ApiResponse<IsBlockedResponseDto>>(unauthorizedResult.Value);
    }

    // ======================== GET BLOCKED USERS TESTS ========================

    [Fact]
    public async Task GetBlockedUsers_WithBlocks_ReturnsList()
    {
        var blocks = new List<UserBlock>
        {
            new() { BlockerId = TestUserId, BlockedUserId = "user-1", CreatedAt = DateTime.UtcNow },
            new() { BlockerId = TestUserId, BlockedUserId = "user-2", CreatedAt = DateTime.UtcNow }
        };
        _mockSafetyService.Setup(s => s.GetBlockedUsersAsync(TestUserId))
            .ReturnsAsync(blocks);

        var result = await _controller.GetBlockedUsers();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<List<BlockResponseDto>>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal(2, response.Data!.Count);
        Assert.Equal("user-1", response.Data[0].BlockedUserId);
        Assert.Equal("user-2", response.Data[1].BlockedUserId);
    }

    [Fact]
    public async Task GetBlockedUsers_NoBlocks_ReturnsEmptyList()
    {
        _mockSafetyService.Setup(s => s.GetBlockedUsersAsync(TestUserId))
            .ReturnsAsync(new List<UserBlock>());

        var result = await _controller.GetBlockedUsers();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<List<BlockResponseDto>>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Empty(response.Data!);
    }

    [Fact]
    public async Task GetBlockedUsers_NoAuth_ReturnsUnauthorized()
    {
        SetupNoAuth();

        var result = await _controller.GetBlockedUsers();

        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.IsType<ApiResponse<List<BlockResponseDto>>>(unauthorizedResult.Value);
    }

    // ======================== SUBMIT REPORT TESTS ========================

    [Fact]
    public async Task SubmitReport_ValidUserReport_ReturnsOk()
    {
        var request = new SafetyReportDto
        {
            SubjectType = "user",
            SubjectId = "bad-user-id",
            Reason = "Harassment",
            Description = "This user sent threatening messages"
        };

        var report = new SafetyReport
        {
            Id = "report-001",
            ReporterId = TestUserId,
            SubjectType = "user",
            SubjectId = "bad-user-id",
            Reason = "Harassment",
            Description = "This user sent threatening messages",
            Status = "Open",
            CreatedAt = DateTime.UtcNow
        };

        _mockSafetyService.Setup(s => s.SubmitReportAsync(
                TestUserId, "user", "bad-user-id", "Harassment", "This user sent threatening messages"))
            .ReturnsAsync(report);

        var result = await _controller.SubmitReport(request);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<SafetyReportResponseDto>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal("report-001", response.Data!.ReportId);
        Assert.Equal("user", response.Data.SubjectType);
        Assert.Equal("Open", response.Data.Status);
    }

    [Fact]
    public async Task SubmitReport_MessageReport_ReturnsOk()
    {
        var request = new SafetyReportDto
        {
            SubjectType = "message",
            SubjectId = "msg-123",
            Reason = "Spam"
        };

        var report = new SafetyReport
        {
            ReporterId = TestUserId,
            SubjectType = "message",
            SubjectId = "msg-123",
            Reason = "Spam"
        };

        _mockSafetyService.Setup(s => s.SubmitReportAsync(
                TestUserId, "message", "msg-123", "Spam", null))
            .ReturnsAsync(report);

        var result = await _controller.SubmitReport(request);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<SafetyReportResponseDto>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal("message", response.Data!.SubjectType);
    }

    [Fact]
    public async Task SubmitReport_PhotoReport_ReturnsOk()
    {
        var request = new SafetyReportDto
        {
            SubjectType = "photo",
            SubjectId = "photo-456",
            Reason = "Inappropriate content"
        };

        var report = new SafetyReport
        {
            ReporterId = TestUserId,
            SubjectType = "photo",
            SubjectId = "photo-456",
            Reason = "Inappropriate content"
        };

        _mockSafetyService.Setup(s => s.SubmitReportAsync(
                TestUserId, "photo", "photo-456", "Inappropriate content", null))
            .ReturnsAsync(report);

        var result = await _controller.SubmitReport(request);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<SafetyReportResponseDto>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal("photo", response.Data!.SubjectType);
    }

    [Fact]
    public async Task SubmitReport_InvalidSubjectType_ReturnsBadRequest()
    {
        var request = new SafetyReportDto
        {
            SubjectType = "invalid",
            SubjectId = "some-id",
            Reason = "Test"
        };

        var result = await _controller.SubmitReport(request);

        var badResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<SafetyReportResponseDto>>(badResult.Value);
        Assert.False(response.Success);
        Assert.Equal("INVALID_SUBJECT_TYPE", response.ErrorCode);
    }

    [Fact]
    public async Task SubmitReport_NoAuth_ReturnsUnauthorized()
    {
        SetupNoAuth();
        var request = new SafetyReportDto
        {
            SubjectType = "user",
            SubjectId = "some-id",
            Reason = "Testing"
        };

        var result = await _controller.SubmitReport(request);

        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.IsType<ApiResponse<SafetyReportResponseDto>>(unauthorizedResult.Value);
    }

    // ======================== GET REPORTS (AUDIT) TESTS ========================

    [Fact]
    public async Task GetReports_WithReports_ReturnsList()
    {
        var reports = new List<SafetyReport>
        {
            new() { Id = "r1", ReporterId = "u1", SubjectType = "user", SubjectId = "u2", Reason = "Spam", Status = "Open" },
            new() { Id = "r2", ReporterId = "u3", SubjectType = "photo", SubjectId = "p1", Reason = "Inappropriate", Status = "Open" }
        };
        _mockSafetyService.Setup(s => s.GetReportsAsync(1, 25))
            .ReturnsAsync(reports);

        var result = await _controller.GetReports();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<List<SafetyReportResponseDto>>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal(2, response.Data!.Count);
        Assert.Equal("r1", response.Data[0].ReportId);
        Assert.Equal("r2", response.Data[1].ReportId);
    }

    [Fact]
    public async Task GetReports_EmptyList_ReturnsEmptyList()
    {
        _mockSafetyService.Setup(s => s.GetReportsAsync(1, 25))
            .ReturnsAsync(new List<SafetyReport>());

        var result = await _controller.GetReports();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<List<SafetyReportResponseDto>>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Empty(response.Data!);
    }

    [Fact]
    public async Task GetReports_CustomPagination_PassesCorrectParams()
    {
        _mockSafetyService.Setup(s => s.GetReportsAsync(3, 10))
            .ReturnsAsync(new List<SafetyReport>());

        var result = await _controller.GetReports(page: 3, pageSize: 10);

        _mockSafetyService.Verify(s => s.GetReportsAsync(3, 10), Times.Once);
    }
}
