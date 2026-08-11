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

public class SupportControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly SupportController _controller;
    private readonly Guid _testUserId = Guid.NewGuid();

    public SupportControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        var logger = new Mock<ILogger<SupportController>>();
        _controller = new SupportController(_context, logger.Object);
        SetUser(_testUserId);
    }

    private void SetUser(Guid userId)
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    private SupportTicket SeedTicket(Guid ownerId, string ticketId, string subject)
    {
        var ticket = new SupportTicket
        {
            TicketId = ticketId,
            UserId = ownerId,
            Subject = subject,
            Description = "details",
            Category = SupportTicketCategory.Bug,
            Status = TicketStatus.Open,
            CreatedAt = DateTime.UtcNow
        };
        _context.SupportTickets.Add(ticket);
        return ticket;
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task SubmitFeedback_ValidRequest_PersistsAndReturnsCreated()
    {
        var request = new CreateSupportTicketRequest(
            SupportTicketCategory.Bug, "App crashes", "Crashes on login", "me@example.com");

        var result = await _controller.SubmitFeedback(request) as CreatedAtActionResult;

        Assert.NotNull(result);
        var response = Assert.IsType<ApiResponse<SupportTicketResponse>>(result!.Value);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.StartsWith("TKT-", response.Data!.TicketId);
        Assert.Equal(TicketStatus.Open, response.Data.Status);
        Assert.Equal(SupportTicketCategory.Bug, response.Data.Category);
        Assert.Equal(_testUserId.ToString(), response.Data.UserId);
        Assert.Single(_context.SupportTickets);
    }

    [Fact]
    public async Task SubmitFeedback_BlankSubject_ReturnsBadRequestAndPersistsNothing()
    {
        var request = new CreateSupportTicketRequest(SupportTicketCategory.Bug, "   ", "details");

        var result = await _controller.SubmitFeedback(request);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(_context.SupportTickets);
    }

    [Fact]
    public async Task SubmitFeedback_BlankDescription_ReturnsBadRequest()
    {
        var request = new CreateSupportTicketRequest(SupportTicketCategory.Bug, "Subject", "  ");

        var result = await _controller.SubmitFeedback(request);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task SubmitFeedback_NoUserClaim_ReturnsUnauthorized()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };
        var request = new CreateSupportTicketRequest(SupportTicketCategory.Bug, "Subject", "details");

        var result = await _controller.SubmitFeedback(request);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task GetMyTickets_ReturnsOnlyOwnTickets()
    {
        SeedTicket(_testUserId, "TKT-AAAA1111", "Mine");
        SeedTicket(Guid.NewGuid(), "TKT-BBBB2222", "Theirs");
        await _context.SaveChangesAsync();

        var result = await _controller.GetMyTickets() as OkObjectResult;

        Assert.NotNull(result);
        var response = Assert.IsType<ApiResponse<List<SupportTicketResponse>>>(result!.Value);
        Assert.NotNull(response.Data);
        Assert.Single(response.Data!);
        Assert.Equal("Mine", response.Data![0].Subject);
    }

    [Fact]
    public async Task GetTicket_OwnTicket_ReturnsTicket()
    {
        SeedTicket(_testUserId, "TKT-CCCC3333", "Mine");
        await _context.SaveChangesAsync();

        var result = await _controller.GetTicket("TKT-CCCC3333") as OkObjectResult;

        Assert.NotNull(result);
        var response = Assert.IsType<ApiResponse<SupportTicketResponse>>(result!.Value);
        Assert.Equal("Mine", response.Data!.Subject);
    }

    [Fact]
    public async Task GetTicket_OtherUsersTicket_ReturnsNotFound()
    {
        SeedTicket(Guid.NewGuid(), "TKT-DDDD4444", "Theirs");
        await _context.SaveChangesAsync();

        var result = await _controller.GetTicket("TKT-DDDD4444");

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetTicket_NonexistentTicket_ReturnsNotFound()
    {
        var result = await _controller.GetTicket("TKT-NOPE0000");

        Assert.IsType<NotFoundObjectResult>(result);
    }
}
