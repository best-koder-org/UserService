using MediatR;
using UserService.Common;
using UserService.DTOs;
using UserService.Models;

namespace UserService.Commands;

/// <summary>
/// Command to update a specific wizard step
/// </summary>
public record UpdateWizardStepCommand : IRequest<Result<UserProfileDetailDto>>
{
    public Guid UserId { get; init; }
    public string? Email { get; init; } // From JWT claims
    public int Step { get; init; } // 1–5

    // Step-specific data (only one will be populated)
    public WizardStepBasicInfoDto? BasicInfo { get; init; }
    public WizardStepPreferencesDto? Preferences { get; init; }
    public WizardStepPhotosDto? Photos { get; init; }
    public WizardStepIdentityDto? Identity { get; init; }
    public WizardStepAboutMeDto? AboutMe { get; init; }
}
