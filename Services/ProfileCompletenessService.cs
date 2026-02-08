namespace UserService.DTOs;

/// <summary>
/// Response DTO for profile completeness
/// </summary>
public record ProfileCompletenessDto(
    int Percentage,
    List<FieldStatusDto> FilledFields,
    List<FieldStatusDto> MissingFields,
    string NextSuggestion
);

/// <summary>
/// Individual field completion status
/// </summary>
public record FieldStatusDto(
    string FieldName,
    bool IsFilled,
    int Weight
);
