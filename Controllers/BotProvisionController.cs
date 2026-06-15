using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserService.Common;
using UserService.Commands;
using UserService.Data;
using UserService.DTOs;

namespace UserService.Controllers
{
    /// <summary>
    /// JWT-authenticated provisioning endpoint for bot accounts.
    /// Only accepts requests with @bot.local emails and valid bot tokens.
    /// Idempotent: if a profile exists for this email, it updates the KeycloakId
    /// to match the caller's JWT sub claim.
    /// </summary>
    [Route("api/bot")]
    [ApiController]
    [Authorize]
    public class BotProvisionController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<BotProvisionController> _logger;

        public BotProvisionController(
            ApplicationDbContext context,
            ILogger<BotProvisionController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Provision or reconcile a bot profile.
        /// Uses the caller's JWT to identify the KeycloakId.
        /// Only operates on @bot.local emails (real user data is never touched).
        /// Returns the profile id.
        /// </summary>
        [HttpPost("provision")]
        public async Task<ActionResult<ApiResponse<BotProvisionResultDto>>> Provision([FromBody] BotProvisionDto dto)
        {
            // ── Extract the caller's KeycloakId from JWT ──
            var keycloakIdStr = User.FindFirst("sub")?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(keycloakIdStr) || !Guid.TryParse(keycloakIdStr, out var keycloakId))
            {
                return Unauthorized(ApiResponse<BotProvisionResultDto>.FailureResult(
                    "Missing or invalid sub claim", "INVALID_TOKEN"));
            }

            // ── Guard: only @bot.local emails ──
            var email = dto.Email?.Trim().ToLowerInvariant() ?? "";
            if (!email.EndsWith("@bot.local", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Bot provision rejected for non-bot email {Email}", email);
                return BadRequest(ApiResponse<BotProvisionResultDto>.FailureResult(
                    "Only @bot.local emails are allowed", "FORBIDDEN_EMAIL"));
            }

            // ── Idempotent lookup: does profile already exist for this email? ──
            var existing = await _context.UserProfiles
                .FirstOrDefaultAsync(p => p.Email == email);

            if (existing != null)
            {
                // Profile exists — bind it to this KeycloakId if needed
                if (existing.UserId != keycloakId)
                {
                    _logger.LogInformation(
                        "Re-binding bot profile {ProfileId} from KeycloakId {Old} to {New}",
                        existing.Id, existing.UserId, keycloakId);
                    existing.UserId = keycloakId;
                    existing.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }

                return Ok(ApiResponse<BotProvisionResultDto>.SuccessResult(
                    new BotProvisionResultDto
                    {
                        ProfileId = existing.Id,
                        KeycloakId = existing.UserId.ToString(),
                        Created = false
                    },
                    "Bot profile reconciled"));
            }

            // ── No existing profile — create one ──
            var birthYear = DateTime.UtcNow.Year - dto.Age;
            var command = new CreateUserProfileCommand
            {
                Name = dto.Name,
                Email = email,
                Bio = dto.Bio ?? "",
                Gender = dto.Gender ?? "male",
                Preferences = dto.Preferences ?? "female",
                DateOfBirth = new DateTime(birthYear, 6, 15),
                City = dto.City ?? "",
                Occupation = dto.Occupation ?? "",
                Education = dto.Education ?? "",
                Interests = dto.Interests ?? new List<string>(),
                Languages = dto.Languages ?? new List<string>(),
                Height = dto.Height,
                SmokingStatus = dto.SmokingStatus ?? "",
                DrinkingStatus = dto.DrinkingStatus ?? "",
                RelationshipType = dto.RelationshipType ?? "",
                UserId = keycloakId
            };

            command.IsBot = true;

            // Use MediatR to send the existing create command
            var mediator = HttpContext.RequestServices.GetRequiredService<MediatR.IMediator>();
            var result = await mediator.Send(command);

            if (result.IsFailure)
            {
                _logger.LogError("Failed to create bot profile for {Email}: {Error}", email, result.Error);
                return BadRequest(ApiResponse<BotProvisionResultDto>.FailureResult(
                    result.Error ?? "Failed to create profile", "PROVISION_FAILED"));
            }

            _logger.LogInformation("Created new bot profile {ProfileId} for {Email}", result.Value!.Id, email);

            return Ok(ApiResponse<BotProvisionResultDto>.SuccessResult(
                new BotProvisionResultDto
                {
                    ProfileId = result.Value!.Id,
                    KeycloakId = result.Value.KeycloakId,
                    Created = true
                },
                "Bot profile created"));
        }
    }

    public class BotProvisionDto
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int Age { get; set; } = 25;
        public string? Bio { get; set; }
        public string? Gender { get; set; }
        public string? Preferences { get; set; }
        public string? City { get; set; }
        public string? Occupation { get; set; }
        public string? Education { get; set; }
        public List<string>? Interests { get; set; }
        public List<string>? Languages { get; set; }
        public int Height { get; set; }
        public string? SmokingStatus { get; set; }
        public string? DrinkingStatus { get; set; }
        public string? RelationshipType { get; set; }
    }

    public class BotProvisionResultDto
    {
        public int ProfileId { get; set; }
        public string KeycloakId { get; set; } = string.Empty;
        public bool Created { get; set; }
    }
}
