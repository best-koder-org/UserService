using System.ComponentModel.DataAnnotations;

namespace UserService.DTOs;

public record GetPreferencesResponse
{
    public int MinAge { get; init; }
    public int MaxAge { get; init; }
    public int MaxDistanceKm { get; init; }
    public string PreferredGender { get; init; } = string.Empty;
    public List<string> RelationshipGoals { get; init; } = new();

    public bool DealBreakerSmoking { get; init; }
    public bool DealBreakerDrinking { get; init; }
    public bool DealBreakerHasChildren { get; init; }
    public bool DealBreakerWantsChildren { get; init; }

    public bool RequireSameReligion { get; init; }
    public bool PreferSimilarEducation { get; init; }
    public bool ShowMeInDiscovery { get; init; }

    public int? MinHeightCm { get; init; }
    public int? MaxHeightCm { get; init; }
    public List<string> PreferredEthnicities { get; init; } = new();
    public List<string> MustHaveInterests { get; init; } = new();

    public DateTime UpdatedAt { get; init; }
}

public record UpdatePreferencesRequest
{
    [Range(18, 100)]
    public int? MinAge { get; init; }

    [Range(18, 100)]
    public int? MaxAge { get; init; }

    [Range(1, 500)]
    public int? MaxDistanceKm { get; init; }

    [StringLength(50)]
    public string? PreferredGender { get; init; }

    public List<string>? RelationshipGoals { get; init; }

    public bool? DealBreakerSmoking { get; init; }
    public bool? DealBreakerDrinking { get; init; }
    public bool? DealBreakerHasChildren { get; init; }
    public bool? DealBreakerWantsChildren { get; init; }

    public bool? RequireSameReligion { get; init; }
    public bool? PreferSimilarEducation { get; init; }
    public bool? ShowMeInDiscovery { get; init; }

    [Range(0, 300)]
    public int? MinHeightCm { get; init; }

    [Range(0, 300)]
    public int? MaxHeightCm { get; init; }

    public List<string>? PreferredEthnicities { get; init; }
    public List<string>? MustHaveInterests { get; init; }
}
