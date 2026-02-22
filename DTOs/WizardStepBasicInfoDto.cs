namespace UserService.DTOs;

/// <summary>
/// Step 1: Basic profile information (name, DOB, gender)
/// </summary>
public class WizardStepBasicInfoDto
{
    public required string FirstName { get; set; }
    public string LastName { get; set; } = string.Empty; // Optional — dating apps don't collect last names
    public required DateTime DateOfBirth { get; set; }
    public required string Gender { get; set; }

    /// <summary>
    /// Validation: Age must be 18+, firstName required
    /// </summary>
    public bool IsValid() =>
        !string.IsNullOrWhiteSpace(FirstName) &&
        DateOfBirth < DateTime.UtcNow.AddYears(-18);
}
