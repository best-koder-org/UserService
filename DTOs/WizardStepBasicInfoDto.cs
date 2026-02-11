namespace UserService.DTOs;

/// <summary>
/// Step 1: Basic profile information (name, DOB, gender)
/// </summary>
public class WizardStepBasicInfoDto
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required DateTime DateOfBirth { get; set; }
    public required string Gender { get; set; }

    /// <summary>
    /// Validation: Age must be 18+
    /// </summary>
    public bool IsValid() =>
        !string.IsNullOrWhiteSpace(FirstName) &&
        !string.IsNullOrWhiteSpace(LastName) &&
        DateOfBirth < DateTime.UtcNow.AddYears(-18);
}
