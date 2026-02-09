namespace UserService.DTOs;

public record VerificationStatusDto
{
    public string UserId { get; init; } = string.Empty;
    public bool PhotoVerified { get; init; }
    public bool EmailVerified { get; init; }
    public bool PhoneVerified { get; init; }
    public VerificationLevel OverallLevel { get; init; } = VerificationLevel.None;
}

public enum VerificationLevel
{
    None = 0,
    Basic = 1,
    Full = 2
}
