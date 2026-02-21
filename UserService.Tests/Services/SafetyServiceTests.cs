using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using UserService.Services;

namespace UserService.Tests.Services;

public class SafetyServiceTests
{
    private readonly SafetyService _service;

    public SafetyServiceTests()
    {
        _service = new SafetyService(Mock.Of<ILogger<SafetyService>>());
    }

    // ===== BlockUserAsync =====

    [Fact]
    public async Task BlockUser_ReturnsTrue()
    {
        var result = await _service.BlockUserAsync("alice", "bob");
        Assert.True(result);
    }

    [Fact]
    public async Task BlockUser_IsDetectedBidirectionally()
    {
        await _service.BlockUserAsync("alice", "bob");

        // alice blocked bob — both directions should report blocked
        Assert.True(await _service.IsBlockedAsync("alice", "bob"));
        Assert.True(await _service.IsBlockedAsync("bob", "alice"));
    }

    [Fact]
    public async Task IsBlocked_NoBlock_ReturnsFalse()
    {
        Assert.False(await _service.IsBlockedAsync("alice", "bob"));
    }

    // ===== UnblockUserAsync =====

    [Fact]
    public async Task UnblockUser_AfterBlock_ReturnsTrue()
    {
        await _service.BlockUserAsync("alice", "bob");

        var result = await _service.UnblockUserAsync("alice", "bob");

        Assert.True(result);
    }

    [Fact]
    public async Task UnblockUser_NoBlockExists_ReturnsFalse()
    {
        var result = await _service.UnblockUserAsync("alice", "bob");

        Assert.False(result);
    }

    [Fact]
    public async Task UnblockUser_AfterUnblock_IsNotBlocked()
    {
        await _service.BlockUserAsync("alice", "bob");
        await _service.UnblockUserAsync("alice", "bob");

        Assert.False(await _service.IsBlockedAsync("alice", "bob"));
    }

    // ===== GetBlockedUsersAsync =====

    [Fact]
    public async Task GetBlockedUsers_ReturnsBlockedList()
    {
        await _service.BlockUserAsync("alice", "bob");
        await _service.BlockUserAsync("alice", "charlie");

        var blocked = await _service.GetBlockedUsersAsync("alice");

        Assert.Equal(2, blocked.Count);
    }

    [Fact]
    public async Task GetBlockedUsers_NoBlocks_ReturnsEmpty()
    {
        var blocked = await _service.GetBlockedUsersAsync("nobody");

        Assert.Empty(blocked);
    }

    // ===== SubmitReportAsync =====

    [Fact]
    public async Task SubmitReport_CreatesReport()
    {
        var report = await _service.SubmitReportAsync("alice", "user", "bob", "Harassment", "He was rude");

        Assert.NotNull(report);
        Assert.Equal("alice", report.ReporterId);
        Assert.Equal("user", report.SubjectType);
        Assert.Equal("bob", report.SubjectId);
        Assert.Equal("Harassment", report.Reason);
        Assert.Equal("He was rude", report.Description);
        Assert.Equal("Open", report.Status);
    }

    [Fact]
    public async Task SubmitReport_AssignsUniqueId()
    {
        var r1 = await _service.SubmitReportAsync("alice", "user", "bob", "Spam", null);
        var r2 = await _service.SubmitReportAsync("alice", "user", "charlie", "Spam", null);

        Assert.NotEqual(r1.Id, r2.Id);
    }

    // ===== GetReportsAsync =====

    [Fact]
    public async Task GetReports_ReturnsPaginated()
    {
        // Submit 30 reports
        for (int i = 0; i < 30; i++)
        {
            await _service.SubmitReportAsync("reporter", "user", $"target{i}", "Reason", null);
        }

        var page1 = await _service.GetReportsAsync(1, 10);
        var page2 = await _service.GetReportsAsync(2, 10);

        Assert.Equal(10, page1.Count);
        Assert.Equal(10, page2.Count);
    }

    [Fact]
    public async Task GetReports_NoReports_ReturnsEmpty()
    {
        var reports = await _service.GetReportsAsync();

        Assert.Empty(reports);
    }
}
