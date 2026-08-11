using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserService.Data;
using UserService.Models;

namespace UserService.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly ApplicationDbContext _ctx;

    public NotificationsController(ApplicationDbContext ctx) => _ctx = ctx;

    private string GetUserId() =>
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? throw new UnauthorizedAccessException();

    /// <summary>
    /// GET /api/notifications/sparks — returns unread sparks for the current user.
    /// Marks them as read when fetched.
    /// </summary>
    [HttpGet("sparks")]
    public async Task<IActionResult> GetSparks([FromQuery] DateTime? since)
    {
        var userId = GetUserId();
        var check = since ?? DateTime.UtcNow.AddDays(-7);

        // Check notification preferences
        var prefs = await _ctx.NotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId.ToString() == userId);
        if (prefs != null && !prefs.SparkNotifications)
            return Ok(new { sparks = Array.Empty<object>(), sparkNotificationsEnabled = false });

        var sparks = await _ctx.Set<SparkRecord>()
            .Where(s => s.RecipientUserId == userId && !s.IsRead && s.CreatedAt >= check)
            .OrderByDescending(s => s.CreatedAt)
            .Take(50)
            .Select(s => new
            {
                s.Id,
                s.SenderUserId,
                s.Message,
                s.CreatedAt,
            })
            .ToListAsync();

        // Mark as read
        var ids = sparks.Select(s => s.Id).ToList();
        if (ids.Count > 0)
        {
            var records = await _ctx.Set<SparkRecord>().Where(s => ids.Contains(s.Id)).ToListAsync();
            foreach (var r in records) r.IsRead = true;
            await _ctx.SaveChangesAsync();
        }

        return Ok(new { sparks, count = sparks.Count });
    }
}
