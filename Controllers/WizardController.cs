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
        
        var command = new UpdateWizardStepCommand
        {
            UserId = userId,
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
        
        var command = new UpdateWizardStepCommand
        {
            UserId = userId,
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
        
        var command = new UpdateWizardStepCommand
        {
            UserId = userId,
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
}
