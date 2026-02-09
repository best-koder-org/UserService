namespace UserService.DTOs;

/// <summary>
/// Verification level representing the degree of profile verification.
/// </summary>
public enum VerificationLevel
{
    None = 0,
    Basic = 1,    // Email verified
    Full = 2      // Email + Photo + Phone verified
}

/// <summary>
/// DTO representing the verification status of a user's profile.
/// </summary>
public class VerificationStatusDto
{
    public bool PhotoVerified { get; set; }
    public bool EmailVerified { get; set; }
    public bool PhoneVerified { get; set; }
    public VerificationLevel OverallLevel { get; set; }
    public DateTime? LastVerifiedAt { get; set; }
    public string? VerificationNote { get; set; }

    /// <summary>
    /// Compute the overall verification level from individual statuses.
    /// </summary>
    public static VerificationLevel ComputeLevel(bool photo, bool email, bool phone)
    {
        if (photo && email && phone) return VerificationLevel.Full;
        if (email) return VerificationLevel.Basic;
        return VerificationLevel.None;
    }
}

/// <summary>
/// Response after submitting a verification request.
/// </summary>
public class VerificationRequestResponseDto
{
    public string UserId { get; set; } = string.Empty;
    public string VerificationType { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public string? Instructions { get; set; }
}
