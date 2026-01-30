namespace UserService.DTOs;

/// <summary>
/// DTO for updating user profile after onboarding
/// All fields are optional - only provided fields will be updated
/// </summary>
public class UpdateProfileDto
{
    public string? Bio { get; set; }
    public string? Gender { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? Occupation { get; set; }
    public string? Company { get; set; }
    public string? Education { get; set; }
    public string? School { get; set; }
    public int? Height { get; set; }
    public string? Religion { get; set; }
    public string? Ethnicity { get; set; }
    public string? SmokingStatus { get; set; }
    public string? DrinkingStatus { get; set; }
}
