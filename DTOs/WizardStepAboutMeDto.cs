namespace UserService.DTOs;

/// <summary>
/// Step 5: About Me — lifestyle, interests, occupation, education.
/// All fields optional — user may skip these screens.
/// </summary>
public class WizardStepAboutMeDto
{
    /// <summary>
    /// Free-form interest tags, e.g. ["Travel", "Cooking", "Hiking"]
    /// Stored as JSON in DB (max 2000 chars).
    /// </summary>
    public List<string> Interests { get; set; } = new();

    /// <summary>
    /// Lifestyle: smoking habit — "Never", "Sometimes", "Regularly"
    /// </summary>
    public string? SmokingStatus { get; set; }

    /// <summary>
    /// Lifestyle: drinking habit — "Never", "Socially", "Regularly"
    /// </summary>
    public string? DrinkingStatus { get; set; }

    /// <summary>
    /// Lifestyle: wants children — null means "prefer not to say"
    /// </summary>
    public bool? WantsChildren { get; set; }

    /// <summary>
    /// Job title, e.g. "Software Engineer"
    /// </summary>
    public string? Occupation { get; set; }

    /// <summary>
    /// Company / employer name
    /// </summary>
    public string? Company { get; set; }

    /// <summary>
    /// Education level, e.g. "Bachelor's", "Master's", "PhD"
    /// </summary>
    public string? Education { get; set; }

    /// <summary>
    /// School / university name
    /// </summary>
    public string? School { get; set; }

    /// <summary>
    /// Always valid — all fields are optional.
    /// </summary>
    public bool IsValid() => true;
}
