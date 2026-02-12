using UserService.DTOs;
using Xunit;

namespace UserService.Tests.DTOs;

public class SupportTicketDtoTests
{
    // ── SupportTicketCategory enum ──
    [Fact]
    public void SupportTicketCategory_Has_Five_Values()
    {
        Assert.Equal(5, Enum.GetValues<SupportTicketCategory>().Length);
    }

    [Theory]
    [InlineData(SupportTicketCategory.Bug)]
    [InlineData(SupportTicketCategory.Feature)]
    [InlineData(SupportTicketCategory.Account)]
    [InlineData(SupportTicketCategory.Safety)]
    [InlineData(SupportTicketCategory.Other)]
    public void SupportTicketCategory_Contains_Expected(SupportTicketCategory cat)
    {
        Assert.True(Enum.IsDefined(cat));
    }

    // ── TicketStatus enum ──
    [Fact]
    public void TicketStatus_Has_Four_Values()
    {
        Assert.Equal(4, Enum.GetValues<TicketStatus>().Length);
    }

    [Theory]
    [InlineData(TicketStatus.Open)]
    [InlineData(TicketStatus.InProgress)]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public void TicketStatus_Contains_Expected(TicketStatus status)
    {
        Assert.True(Enum.IsDefined(status));
    }

    // ── CreateSupportTicketRequest ──
    [Fact]
    public void CreateSupportTicketRequest_Creates_Without_Email()
    {
        var req = new CreateSupportTicketRequest(SupportTicketCategory.Bug, "Crash on login", "App crashes when I tap login");
        Assert.Equal(SupportTicketCategory.Bug, req.Category);
        Assert.Equal("Crash on login", req.Subject);
        Assert.Equal("App crashes when I tap login", req.Description);
        Assert.Null(req.ContactEmail);
    }

    [Fact]
    public void CreateSupportTicketRequest_Creates_With_Email()
    {
        var req = new CreateSupportTicketRequest(
            SupportTicketCategory.Feature,
            "Dark mode",
            "Please add dark mode",
            "user@example.com"
        );
        Assert.Equal("user@example.com", req.ContactEmail);
    }

    [Fact]
    public void CreateSupportTicketRequest_ContactEmail_Defaults_Null()
    {
        var req = new CreateSupportTicketRequest(SupportTicketCategory.Account, "Delete my data", "GDPR request");
        Assert.Null(req.ContactEmail);
    }

    // ── SupportTicketResponse ──
    [Fact]
    public void SupportTicketResponse_Full_Construction()
    {
        var now = DateTime.UtcNow;
        var resp = new SupportTicketResponse(
            TicketId: "TKT-001",
            UserId: "user-789",
            Category: SupportTicketCategory.Safety,
            Status: TicketStatus.Open,
            Subject: "Harassment",
            Description: "User sent threatening messages",
            ContactEmail: null,
            CreatedAt: now,
            ResolvedAt: null
        );

        Assert.Equal("TKT-001", resp.TicketId);
        Assert.Equal("user-789", resp.UserId);
        Assert.Equal(SupportTicketCategory.Safety, resp.Category);
        Assert.Equal(TicketStatus.Open, resp.Status);
        Assert.Null(resp.ResolvedAt);
    }

    [Fact]
    public void SupportTicketResponse_Resolved_Has_ResolvedAt()
    {
        var created = DateTime.UtcNow.AddDays(-3);
        var resolved = DateTime.UtcNow;
        var resp = new SupportTicketResponse(
            "TKT-002", "user-1", SupportTicketCategory.Bug, TicketStatus.Resolved,
            "Fixed", "Was a bug", "u@e.com", created, resolved
        );

        Assert.Equal(TicketStatus.Resolved, resp.Status);
        Assert.Equal(resolved, resp.ResolvedAt);
    }

    [Fact]
    public void SupportTicketResponse_Record_Equality()
    {
        var now = DateTime.UtcNow;
        var a = new SupportTicketResponse("T1", "U1", SupportTicketCategory.Other, TicketStatus.Open, "S", "D", null, now, null);
        var b = new SupportTicketResponse("T1", "U1", SupportTicketCategory.Other, TicketStatus.Open, "S", "D", null, now, null);
        Assert.Equal(a, b);
    }

    [Fact]
    public void SupportTicketResponse_Record_Inequality()
    {
        var now = DateTime.UtcNow;
        var a = new SupportTicketResponse("T1", "U1", SupportTicketCategory.Bug, TicketStatus.Open, "S", "D", null, now, null);
        var b = new SupportTicketResponse("T2", "U1", SupportTicketCategory.Bug, TicketStatus.Open, "S", "D", null, now, null);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void CreateSupportTicketRequest_With_Mutation()
    {
        var original = new CreateSupportTicketRequest(SupportTicketCategory.Bug, "Title", "Desc");
        var modified = original with { Category = SupportTicketCategory.Feature };
        Assert.Equal(SupportTicketCategory.Feature, modified.Category);
        Assert.Equal("Title", modified.Subject);
    }
}
