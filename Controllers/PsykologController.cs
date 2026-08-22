using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Services;

namespace UserService.Controllers;

[ApiController]
[Route("api/psykolog")]
[Authorize]
public class PsykologController : ControllerBase
{
    private readonly IPsykologService _psykolog;
    private readonly IVectorEmbeddingService _vectors;

    public PsykologController(IPsykologService psykolog, IVectorEmbeddingService vectors)
    {
        _psykolog = psykolog;
        _vectors = vectors;
    }

    private string GetUserId() =>
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? string.Empty;

    // POST /api/psykolog/sessions — start new session
    [HttpPost("sessions")]
    public async Task<IActionResult> StartSession()
    {
        var session = await _psykolog.StartSessionAsync(GetUserId());
        if (session == null)
            return StatusCode(429, new { error = "Monthly session limit reached." });
        return Ok(new
        {
            session.Id,
            session.SessionNumber,
            session.StartedAt,
            session.Status
        });
    }

    // POST /api/psykolog/sessions/{id}/messages — send message, receive reply
    [HttpPost("sessions/{id:int}/messages")]
    public async Task<IActionResult> SendMessage(int id, [FromBody] SendMessageRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Content))
            return BadRequest(new { error = "Content is required." });

        var reply = await _psykolog.SendMessageAsync(id, GetUserId(), req.Content, HttpContext.RequestAborted);
        if (reply == null)
            return StatusCode(429, new { error = "Message limit reached or session not found." });
        return Ok(new { content = reply });
    }

    // POST /api/psykolog/sessions/{id}/end — end session + trigger theme extraction
    [HttpPost("sessions/{id:int}/end")]
    public async Task<IActionResult> EndSession(int id)
    {
        var session = await _psykolog.EndSessionAsync(id, GetUserId());
        if (session == null)
            return NotFound();
        return Ok(new
        {
            session.Id,
            session.EndedAt,
            session.Status,
            session.ThemeCount
        });
    }

    // GET /api/psykolog/sessions — list user's sessions
    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions()
    {
        var sessions = await _psykolog.GetSessionsAsync(GetUserId());
        return Ok(sessions.Select(s => new
        {
            s.Id,
            s.SessionNumber,
            s.StartedAt,
            s.EndedAt,
            s.Status,
            s.ThemeCount
        }));
    }

    // GET /api/psykolog/themes — extracted themes for user
    [HttpGet("themes")]
    public async Task<IActionResult> GetThemes()
    {
        var themes = await _psykolog.GetThemesAsync(GetUserId());
        return Ok(themes.Select(t => new
        {
            t.Id,
            t.Label,
            t.Intensity,
            t.Axis,
            t.CreatedAt,
            t.SessionId
        }));
    }

    // GET /api/psykolog/sessions/{id}/messages — re-read a session transcript (owner only)
    [HttpGet("sessions/{id:int}/messages")]
    public async Task<IActionResult> GetMessages(int id)
    {
        var messages = await _psykolog.GetMessagesAsync(id, GetUserId());
        if (messages == null) return NotFound();
        return Ok(messages.Select(m => new
        {
            m.Id,
            Role = m.Role.ToString(),
            m.Content,
            m.CreatedAt
        }));
    }

    // POST /api/psykolog/bio-audit — compare bio against extracted themes (recommendation only)
    [HttpPost("bio-audit")]
    public async Task<IActionResult> BioAudit()
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var suggestions = await _psykolog.BioAuditAsync(userId);
        if (suggestions == null)
            return Ok(new { suggestions = Array.Empty<string>(), note = "Gör en session först (eller fyll i din bio) så kan jag jämföra dina teman med din profil." });

        return Ok(new { suggestions });
    }

    // GET /api/psykolog/vector-similarity/{otherKeycloakId}
    [HttpGet("vector-similarity/{otherKeycloakId}")]
    public async Task<IActionResult> GetVectorSimilarity(string otherKeycloakId)
    {
        var myId = GetUserId();
        if (string.IsNullOrEmpty(myId)) return Unauthorized();

        var similarity = await _vectors.CosineSimilarityAsync(myId, otherKeycloakId);
        if (similarity == null)
            return Ok(new { similarity = (double?)null, reason = "one or both users lack a reflection vector" });

        return Ok(new { similarity = Math.Round(similarity.Value, 4) });
    }

    public record SendMessageRequest(string Content);
}
