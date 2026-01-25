namespace UserService.DTOs;

/// <summary>
/// Step 2: Search preferences (age range, distance, etc.)
/// </summary>
public class WizardStepPreferencesDto
{
    public int MinAge { get; set; } = 18;
    public int MaxAge { get; set; } = 99;
    public int MaxDistance { get; set; } = 50; // km
    public string? PreferredGender { get; set; }
    
    // Optional: Bio text
    public string? Bio { get; set; }
    
    public bool IsValid() =>
        MinAge >= 18 &&
        MaxAge >= MinAge &&
        MaxDistance > 0;
}
