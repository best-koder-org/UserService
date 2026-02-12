namespace UserService.DTOs;

/// <summary>
/// Response DTO for profile completeness — living score that grows over time
/// </summary>
public record ProfileCompletenessDto(
    int Percentage,
    List<FieldStatusDto> FilledFields,
    List<FieldStatusDto> MissingFields,
    string NextSuggestion
);

/// <summary>
/// Individual field completion status with tier classification
/// </summary>
public record FieldStatusDto(
    string FieldName,
    bool IsFilled,
    int Weight,
    string Tier
);
