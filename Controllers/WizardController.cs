using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Commands;
using UserService.Common;
using UserService.DTOs;
using System.Security.Claims;

namespace UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WizardController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<WizardController> _logger;

    public WizardController(IMediator mediator, ILogger<WizardController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Update wizard step 1: Basic profile information
    /// </summary>
    [HttpPatch("step/1")]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateStepBasicInfo([FromBody] WizardStepBasicInfoDto dto)
    {
        var userId = GetUserIdFromClaims();
        var email = GetEmailFromClaims();

        var command = new UpdateWizardStepCommand
        {
            UserId = userId,
            Email = email,
            Step = 1,
            BasicInfo = dto
        };

        var result = await _mediator.Send(command);

        if (result.IsFailure)
        {
            return BadRequest(ApiResponse<UserProfileDetailDto>.FailureResult(result.Error ?? "Failed to update basic info"));
        }

        return Ok(ApiResponse<UserProfileDetailDto>.SuccessResult(result.Value!));
    }

    /// <summary>
    /// Update wizard step 2: Search preferences
    /// </summary>
    [HttpPatch("step/2")]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateStepPreferences([FromBody] WizardStepPreferencesDto dto)
    {
        var userId = GetUserIdFromClaims();
        var email = GetEmailFromClaims();

        var command = new UpdateWizardStepCommand
        {
            UserId = userId,
            Email = email,
            Step = 2,
            Preferences = dto
        };

        var result = await _mediator.Send(command);

        if (result.IsFailure)
        {
            return BadRequest(ApiResponse<UserProfileDetailDto>.FailureResult(result.Error ?? "Failed to update preferences"));
        }

        return Ok(ApiResponse<UserProfileDetailDto>.SuccessResult(result.Value!));
    }

    /// <summary>
    /// Complete wizard step 3: Photos uploaded (marks profile as Ready)
    /// </summary>
    [HttpPatch("step/3")]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CompleteWizard([FromBody] WizardStepPhotosDto dto)
    {
        var userId = GetUserIdFromClaims();
        var email = GetEmailFromClaims();

        var command = new UpdateWizardStepCommand
        {
            UserId = userId,
            Email = email,
            Step = 3,
            Photos = dto
        };

        var result = await _mediator.Send(command);

        if (result.IsFailure)
        {
            return BadRequest(ApiResponse<UserProfileDetailDto>.FailureResult(result.Error ?? "Failed to complete wizard"));
        }

        _logger.LogInformation("User {UserId} completed onboarding wizard", userId);
        return Ok(ApiResponse<UserProfileDetailDto>.SuccessResult(result.Value!));
    }

    /// <summary>
    /// Update wizard step 4: Identity & goals (orientation, relationship type)
    /// Optional — user may skip these screens.
    /// </summary>
    [HttpPatch("step/4")]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateStepIdentity([FromBody] WizardStepIdentityDto dto)
    {
        var userId = GetUserIdFromClaims();
        var email = GetEmailFromClaims();

        var command = new UpdateWizardStepCommand
        {
            UserId = userId,
            Email = email,
            Step = 4,
            Identity = dto
        };

        var result = await _mediator.Send(command);

        if (result.IsFailure)
        {
            return BadRequest(ApiResponse<UserProfileDetailDto>.FailureResult(result.Error ?? "Failed to update identity"));
        }

        return Ok(ApiResponse<UserProfileDetailDto>.SuccessResult(result.Value!));
    }

    /// <summary>
    /// Update wizard step 5: About me (interests, lifestyle, work, education)
    /// Optional — user may skip these screens.
    /// </summary>
    [HttpPatch("step/5")]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateStepAboutMe([FromBody] WizardStepAboutMeDto dto)
    {
        var userId = GetUserIdFromClaims();
        var email = GetEmailFromClaims();

        var command = new UpdateWizardStepCommand
        {
            UserId = userId,
            Email = email,
            Step = 5,
            AboutMe = dto
        };

        var result = await _mediator.Send(command);

        if (result.IsFailure)
        {
            return BadRequest(ApiResponse<UserProfileDetailDto>.FailureResult(result.Error ?? "Failed to update about me"));
        }

        return Ok(ApiResponse<UserProfileDetailDto>.SuccessResult(result.Value!));
    }

    private Guid GetUserIdFromClaims()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid user claims - missing or invalid user ID");
        }

        return userId;
    }

    private string? GetEmailFromClaims()
    {
        // Log all claims for debugging
        _logger.LogInformation("JWT Claims: {Claims}",
            string.Join(", ", User.Claims.Select(c => $"{c.Type}={c.Value}")));

        return User.FindFirst(ClaimTypes.Email)?.Value
               ?? User.FindFirst("email")?.Value;
    }
}
