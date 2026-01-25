namespace UserService.Models;

/// <summary>
/// Represents the user's progress through the onboarding wizard.
/// Matches the client-side enum in Flutter (pending/ready/blocked).
/// </summary>
public enum OnboardingStatus
{
    /// <summary>
    /// Profile creation not yet complete - user in wizard flow
    /// </summary>
    Incomplete = 0,
    
    /// <summary>
    /// Profile ready for matchmaking - all required fields complete
    /// </summary>
    Ready = 1,
    
    /// <summary>
    /// Profile suspended - flagged for moderation or user action required
    /// </summary>
    Suspended = 2
}
