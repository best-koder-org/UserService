using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.DTOs;

namespace UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VerificationController : ControllerBase
{
    [HttpGet("{userId}")]
    public IActionResult GetVerificationStatus(string userId)
    {
        // MVP: Return unverified status — actual verification logic comes later
        var dto = new VerificationStatusDto
        {
            UserId = userId,
            PhotoVerified = false,
            EmailVerified = false,
            PhoneVerified = false,
            OverallLevel = VerificationLevel.None
        };

        return Ok(dto);
    }

    [HttpPost("{userId}/request")]
    public IActionResult RequestVerification(string userId, [FromQuery] string type = "photo")
    {
        // MVP: Accept the request but don't process it yet
        return Accepted(new
        {
            UserId = userId,
            Type = type,
            Status = "pending",
            Message = $"Verification request for {type} received. Processing will be available in a future release."
        });
    }
}
