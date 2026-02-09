namespace UserService.DTOs;

/// <summary>
/// Onboarding funnel metrics — tracks how users progress through registration.
/// Used by admin dashboard to identify drop-off points.
/// </summary>
public class OnboardingMetricsDto
{
    /// <summary>Total registered users (have Keycloak sub).</summary>
    public int TotalRegistered { get; set; }

    /// <summary>Users who completed basic info step.</summary>
    public int CompletedBasicInfo { get; set; }

    /// <summary>Users who uploaded at least one photo.</summary>
    public int CompletedPhotos { get; set; }

    /// <summary>Users who set preferences.</summary>
    public int CompletedPreferences { get; set; }

    /// <summary>Users who completed all onboarding steps.</summary>
    public int FullyOnboarded { get; set; }

    /// <summary>Conversion rate from registration to fully onboarded (0-100%).</summary>
    public double ConversionRate { get; set; }

    /// <summary>When this snapshot was taken.</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Per-step completion breakdown with counts and percentages.
/// </summary>
public class OnboardingStepMetric
{
    public string StepName { get; set; } = string.Empty;
    public int CompletedCount { get; set; }
    public int PendingCount { get; set; }
    public double CompletionPercentage { get; set; }
}
