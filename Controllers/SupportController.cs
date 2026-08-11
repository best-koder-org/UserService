using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserService.Common;
using UserService.Data;
using UserService.DTOs;
using UserService.Models;

namespace UserService.Controllers;

/// <summary>
/// Feedback and support ticket endpoints (T091).
/// Persists tickets so testers and users can report bugs and request help.
/// </summary>
[Route("api/support")]
[ApiController]
[Authorize]
public class SupportController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<SupportController> _logger;

    public SupportController(ApplicationDbContext db, ILogger<SupportController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Submit feedback or a support ticket.</summary>
    [HttpPost("feedback")]
    public async Task<IActionResult> SubmitFeedback([FromBody] CreateSupportTicketRequest request)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse<SupportTicketResponse>.FailureResult("Missing or invalid user identity"));

        if (request is null ||
            string.IsNullOrWhiteSpace(request.Subject) ||
            string.IsNullOrWhiteSpace(request.Description))
        {
            return BadRequest(ApiResponse<SupportTicketResponse>.FailureResult(
                "Subject and description are required"));
        }

        var ticket = new SupportTicket
        {
            TicketId = GenerateTicketId(),
            UserId = userId,
            Category = request.Category,
            Status = TicketStatus.Open,
            Subject = request.Subject.Trim(),
            Description = request.Description.Trim(),
            ContactEmail = string.IsNullOrWhiteSpace(request.ContactEmail) ? null : request.ContactEmail.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _db.SupportTickets.Add(ticket);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Support ticket {TicketId} created by {UserId}: {Category} - {Subject}",
            ticket.TicketId, userId, ticket.Category, ticket.Subject);

        return CreatedAtAction(nameof(GetTicket), new { ticketId = ticket.TicketId },
            ApiResponse<SupportTicketResponse>.SuccessResult(ToResponse(ticket), "Support ticket submitted"));
    }

    /// <summary>List the authenticated user's support tickets, newest first.</summary>
    [HttpGet("my-tickets")]
    public async Task<IActionResult> GetMyTickets()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse<List<SupportTicketResponse>>.FailureResult("Missing or invalid user identity"));

        var tickets = await _db.SupportTickets
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        return Ok(ApiResponse<List<SupportTicketResponse>>.SuccessResult(
            tickets.Select(ToResponse).ToList()));
    }

    /// <summary>Get one of the authenticated user's tickets by its public ticket id.</summary>
    [HttpGet("tickets/{ticketId}")]
    public async Task<IActionResult> GetTicket(string ticketId)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse<SupportTicketResponse>.FailureResult("Missing or invalid user identity"));

        // Ownership is enforced in the query: another user's ticket simply isn't found,
        // which avoids leaking the existence of tickets that aren't the caller's.
        var ticket = await _db.SupportTickets
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TicketId == ticketId && t.UserId == userId);

        if (ticket is null)
            return NotFound(ApiResponse<SupportTicketResponse>.FailureResult("Ticket not found"));

        return Ok(ApiResponse<SupportTicketResponse>.SuccessResult(ToResponse(ticket)));
    }

    private bool TryGetUserId(out Guid userId)
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst("sub")?.Value
                    ?? User.FindFirst("userId")?.Value;
        return Guid.TryParse(claim, out userId);
    }

    private static string GenerateTicketId() =>
        $"TKT-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

    private static SupportTicketResponse ToResponse(SupportTicket t) => new(
        t.TicketId,
        t.UserId.ToString(),
        t.Category,
        t.Status,
        t.Subject,
        t.Description,
        t.ContactEmail,
        t.CreatedAt,
        t.ResolvedAt);
}
