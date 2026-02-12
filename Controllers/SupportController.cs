using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Common;
using UserService.DTOs;

namespace UserService.Controllers;

/// <summary>
/// Feedback & support ticket endpoints (T091 scaffolding).
/// Stubs only — returns 501 NotImplemented until full implementation.
/// </summary>
[Route("api/support")]
[ApiController]
[Authorize]
public class SupportController : ControllerBase
{
    private readonly ILogger<SupportController> _logger;

    public SupportController(ILogger<SupportController> logger)
    {
        _logger = logger;
    }

    /// <summary>Submit feedback or a support ticket.</summary>
    [HttpPost("feedback")]
    public IActionResult SubmitFeedback([FromBody] CreateSupportTicketRequest request)
    {
        // TODO(T091): Persist ticket, send confirmation email, notify support team
        _logger.LogInformation("Support ticket submitted: {Category} - {Subject}", request.Category, request.Subject);
        return StatusCode(501, ApiResponse<string>.FailureResult("Support ticket submission not yet implemented"));
    }

    /// <summary>List the authenticated user's support tickets.</summary>
    [HttpGet("my-tickets")]
    public IActionResult GetMyTickets()
    {
        // TODO(T091): Query tickets by authenticated user ID
        _logger.LogInformation("My tickets requested");
        return StatusCode(501, ApiResponse<string>.FailureResult("My tickets not yet implemented"));
    }

    /// <summary>Get a specific support ticket by ID.</summary>
    [HttpGet("tickets/{ticketId}")]
    public IActionResult GetTicket(string ticketId)
    {
        // TODO(T091): Fetch ticket, verify ownership
        _logger.LogInformation("Ticket {TicketId} requested", ticketId);
        return StatusCode(501, ApiResponse<string>.FailureResult("Ticket retrieval not yet implemented"));
    }
}
