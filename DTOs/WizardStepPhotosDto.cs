namespace UserService.DTOs;

/// <summary>
/// Step 3: Photo upload confirmation (actual upload via PhotoService)
/// </summary>
public class WizardStepPhotosDto
{
    public required List<string> PhotoUrls { get; set; }
    
    /// <summary>
    /// Minimum 1 photo required to complete wizard
    /// </summary>
    public bool IsValid() => PhotoUrls.Count >= 1;
}
