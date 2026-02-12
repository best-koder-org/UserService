using UserService.DTOs;
using Xunit;

namespace UserService.Tests.DTOs;

public class AccountPauseDtoTests
{
    // ── AccountStatus enum ──
    [Fact]
    public void AccountStatus_Has_Four_Values()
    {
        var values = Enum.GetValues<AccountStatus>();
        Assert.Equal(4, values.Length);
    }

    [Theory]
    [InlineData(AccountStatus.Active)]
    [InlineData(AccountStatus.Paused)]
    [InlineData(AccountStatus.Deactivated)]
    [InlineData(AccountStatus.Deleted)]
    public void AccountStatus_Contains_Expected_Value(AccountStatus status)
    {
        Assert.True(Enum.IsDefined(status));
    }

    // ── PauseDuration enum ──
    [Fact]
    public void PauseDuration_Has_Four_Values()
    {
        var values = Enum.GetValues<PauseDuration>();
        Assert.Equal(4, values.Length);
    }

    [Theory]
    [InlineData(PauseDuration.Hours24)]
    [InlineData(PauseDuration.Hours72)]
    [InlineData(PauseDuration.OneWeek)]
    [InlineData(PauseDuration.Indefinite)]
    public void PauseDuration_Contains_Expected_Value(PauseDuration duration)
    {
        Assert.True(Enum.IsDefined(duration));
    }

    // ── AccountPauseRequest ──
    [Fact]
    public void AccountPauseRequest_Creates_With_Duration()
    {
        var request = new AccountPauseRequest(PauseDuration.Hours24);
        Assert.Equal(PauseDuration.Hours24, request.Duration);
        Assert.Null(request.Reason);
    }

    [Fact]
    public void AccountPauseRequest_Creates_With_Duration_And_Reason()
    {
        var request = new AccountPauseRequest(PauseDuration.OneWeek, "vacation");
        Assert.Equal(PauseDuration.OneWeek, request.Duration);
        Assert.Equal("vacation", request.Reason);
    }

    [Fact]
    public void AccountPauseRequest_Reason_Defaults_To_Null()
    {
        var request = new AccountPauseRequest(PauseDuration.Indefinite);
        Assert.Null(request.Reason);
    }

    // ── AccountStatusResponse ──
    [Fact]
    public void AccountStatusResponse_Active_Has_No_Pause_Fields()
    {
        var response = new AccountStatusResponse(
            UserId: "user-123",
            Status: AccountStatus.Active,
            PausedAt: null,
            ResumeAt: null,
            PauseDuration: null,
            PauseReason: null
        );

        Assert.Equal("user-123", response.UserId);
        Assert.Equal(AccountStatus.Active, response.Status);
        Assert.Null(response.PausedAt);
        Assert.Null(response.ResumeAt);
        Assert.Null(response.PauseDuration);
        Assert.Null(response.PauseReason);
    }

    [Fact]
    public void AccountStatusResponse_Paused_Has_All_Fields()
    {
        var now = DateTime.UtcNow;
        var resumeAt = now.AddDays(7);
        var response = new AccountStatusResponse(
            UserId: "user-456",
            Status: AccountStatus.Paused,
            PausedAt: now,
            ResumeAt: resumeAt,
            PauseDuration: PauseDuration.OneWeek,
            PauseReason: "taking a break"
        );

        Assert.Equal("user-456", response.UserId);
        Assert.Equal(AccountStatus.Paused, response.Status);
        Assert.Equal(now, response.PausedAt);
        Assert.Equal(resumeAt, response.ResumeAt);
        Assert.Equal(PauseDuration.OneWeek, response.PauseDuration);
        Assert.Equal("taking a break", response.PauseReason);
    }

    [Fact]
    public void AccountStatusResponse_Record_Equality()
    {
        var a = new AccountStatusResponse("u1", AccountStatus.Active, null, null, null, null);
        var b = new AccountStatusResponse("u1", AccountStatus.Active, null, null, null, null);
        Assert.Equal(a, b);
    }

    [Fact]
    public void AccountStatusResponse_Record_Inequality_DifferentStatus()
    {
        var a = new AccountStatusResponse("u1", AccountStatus.Active, null, null, null, null);
        var b = new AccountStatusResponse("u1", AccountStatus.Paused, null, null, null, null);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void AccountPauseRequest_Record_With_Mutation()
    {
        var original = new AccountPauseRequest(PauseDuration.Hours24, "test");
        var modified = original with { Duration = PauseDuration.Hours72 };
        Assert.Equal(PauseDuration.Hours72, modified.Duration);
        Assert.Equal("test", modified.Reason);
    }
}
