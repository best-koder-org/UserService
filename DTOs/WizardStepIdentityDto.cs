namespace UserService.DTOs;

/// <summary>
/// Step 4: Identity & Goals (orientation, relationship type)
/// All fields optional — user may skip this screen.
/// </summary>
public class WizardStepIdentityDto
{
    /// <summary>
    /// Sexual orientation(s), e.g. "Straight", "Bisexual, Queer"
    /// Stored as comma-separated in DB (max 50 chars).
    /// </summary>
    public string? SexualOrientation { get; set; }

    /// <summary>
    /// What the user is looking for: "Relationship", "Casual", "Friendship", etc.
    /// </summary>
    public string? RelationshipType { get; set; }

    /// <summary>
    /// Always valid — both fields are optional (user may skip).
    /// </summary>
    public bool IsValid() => true;
}
