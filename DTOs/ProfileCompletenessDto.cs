namespace UserService.Services;

/// <summary>
/// Interface for profile completeness calculation.
/// </summary>
public interface IProfileCompletenessService
{
    ProfileCompletenessResult Calculate(string userId, ProfileData data);
}

/// <summary>
/// Profile field status
/// </summary>
public record ProfileFieldStatus(string FieldName, bool IsFilled, int Weight);

/// <summary>
/// Profile completeness calculation result
/// </summary>
public record ProfileCompletenessResult(
    int Percentage,
    List<ProfileFieldStatus> FilledFields,
    List<ProfileFieldStatus> MissingFields,
    string NextSuggestion
);

/// <summary>
/// Input data for completeness calculation (decoupled from EF model)
/// </summary>
public record ProfileData(
    string? FirstName,
    DateTime? Birthday,
    string? Gender,
    string? Bio,
    int PhotoCount,
    string? City,
    List<string>? Interests
);

/// <summary>
/// Calculates profile completeness as a weighted percentage.
/// Each field contributes a specific weight to the overall score.
/// </summary>
public class ProfileCompletenessService : IProfileCompletenessService
{
    private static readonly List<(string Name, int Weight, Func<ProfileData, bool> Check)> Fields = new()
    {
        ("Name",       10, d => !string.IsNullOrWhiteSpace(d.FirstName)),
        ("Birthday",   10, d => d.Birthday.HasValue),
        ("Gender",     10, d => !string.IsNullOrWhiteSpace(d.Gender)),
        ("Bio",        15, d => !string.IsNullOrWhiteSpace(d.Bio) && d.Bio.Length >= 10),
        ("Photo (1+)", 20, d => d.PhotoCount >= 1),
        ("Photos (3+)",10, d => d.PhotoCount >= 3),
        ("Location",   10, d => !string.IsNullOrWhiteSpace(d.City)),
        ("Interests",  15, d => d.Interests != null && d.Interests.Count >= 3),
    };

    public ProfileCompletenessResult Calculate(string userId, ProfileData data)
    {
        var filled = new List<ProfileFieldStatus>();
        var missing = new List<ProfileFieldStatus>();

        foreach (var (name, weight, check) in Fields)
        {
            var status = new ProfileFieldStatus(name, check(data), weight);
            if (status.IsFilled)
                filled.Add(status);
            else
                missing.Add(status);
        }

        var percentage = filled.Sum(f => f.Weight);
        var nextSuggestion = missing.Count > 0
            ? $"Add your {missing.OrderByDescending(m => m.Weight).First().FieldName.ToLower()} to boost your profile!"
            : "Your profile is complete! 🎉";

        return new ProfileCompletenessResult(
            Percentage: percentage,
            FilledFields: filled,
            MissingFields: missing,
            NextSuggestion: nextSuggestion
        );
    }
}
