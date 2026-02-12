using System.Text.Json;
using UserService.Models;

namespace UserService.Services;

/// <summary>
/// Living profile completeness score. Three-tier weighted formula:
/// Required (40%) + Encouraged (35%) + Optional (25%)
/// Score grows as users enrich their profile over time.
/// </summary>
public interface IProfileCompletenessService
{
    ProfileCompletenessResult Calculate(UserProfile profile);
}

public record ProfileFieldStatus(string FieldName, bool IsFilled, int Weight, string Tier);
public record ProfileCompletenessResult(
    int Percentage,
    List<ProfileFieldStatus> FilledFields,
    List<ProfileFieldStatus> MissingFields,
    string NextSuggestion
);

public class ProfileCompletenessService : IProfileCompletenessService
{
    // Required fields — 40% weight
    private static readonly (string Name, Func<UserProfile, bool> Check, int Weight, string Nudge)[] RequiredFields = new[]
    {
        ("Name", (Func<UserProfile, bool>)(p => !string.IsNullOrWhiteSpace(p.Name)), 10, "Add your name to get started"),
        ("Birthday", (Func<UserProfile, bool>)(p => p.DateOfBirth != default), 10, "Add your birthday"),
        ("Gender", (Func<UserProfile, bool>)(p => !string.IsNullOrWhiteSpace(p.Gender)), 10, "Select your gender"),
        ("MinPhotos", (Func<UserProfile, bool>)(p => p.PhotoCount >= 2), 10, "Add at least 2 photos to show yourself"),
    };

    // Encouraged fields — 35% weight (these are nudged strongly)
    private static readonly (string Name, Func<UserProfile, bool> Check, int Weight, string Nudge)[] EncouragedFields = new[]
    {
        ("Bio", (Func<UserProfile, bool>)(p => (p.Bio ?? "").Length >= 50), 8, "Write a bio (50+ chars) to get 15% more matches!"),
        ("Interests5", (Func<UserProfile, bool>)(p => CountJsonArray(p.Interests) >= 5), 7, "Add 5+ interests so we can find your people"),
        ("Height", (Func<UserProfile, bool>)(p => p.Height > 0), 5, "Add your height"),
        ("RelationshipGoals", (Func<UserProfile, bool>)(p => !string.IsNullOrWhiteSpace(p.RelationshipType)), 5, "What are you looking for?"),
        ("BioLong", (Func<UserProfile, bool>)(p => (p.Bio ?? "").Length >= 150), 5, "Expand your bio to 150+ chars for maximum visibility"),
        ("Photos4Plus", (Func<UserProfile, bool>)(p => p.PhotoCount >= 4), 5, "Add more photos — profiles with 4+ get 2x more likes"),
    };

    // Optional fields — 25% weight (nice to have)
    private static readonly (string Name, Func<UserProfile, bool> Check, int Weight, string Nudge)[] OptionalFields = new[]
    {
        ("Occupation", (Func<UserProfile, bool>)(p => !string.IsNullOrWhiteSpace(p.Occupation)), 3, "Add your job title"),
        ("Education", (Func<UserProfile, bool>)(p => !string.IsNullOrWhiteSpace(p.Education)), 2, "Add your education"),
        ("Smoking", (Func<UserProfile, bool>)(p => !string.IsNullOrWhiteSpace(p.SmokingStatus)), 2, "Share smoking preference"),
        ("Drinking", (Func<UserProfile, bool>)(p => !string.IsNullOrWhiteSpace(p.DrinkingStatus)), 2, "Share drinking preference"),
        ("Religion", (Func<UserProfile, bool>)(p => !string.IsNullOrWhiteSpace(p.Religion)), 2, "Add religion"),
        ("Languages", (Func<UserProfile, bool>)(p => CountJsonArray(p.Languages) > 0), 2, "What languages do you speak?"),
        ("Photos6Max", (Func<UserProfile, bool>)(p => p.PhotoCount >= 6), 3, "Fill all 6 photo slots for the best profile"),
        ("Verified", (Func<UserProfile, bool>)(p => p.IsVerified), 4, "Get verified for a blue badge ✓"),
        ("Company", (Func<UserProfile, bool>)(p => !string.IsNullOrWhiteSpace(p.Company)), 2, "Add your company"),
        ("Instagram", (Func<UserProfile, bool>)(p => !string.IsNullOrWhiteSpace(p.InstagramHandle)), 3, "Link your Instagram"),
    };

    public ProfileCompletenessResult Calculate(UserProfile profile)
    {
        var filled = new List<ProfileFieldStatus>();
        var missing = new List<ProfileFieldStatus>();

        double requiredScore = EvalTier(profile, RequiredFields, "Required", filled, missing);
        double encouragedScore = EvalTier(profile, EncouragedFields, "Encouraged", filled, missing);
        double optionalScore = EvalTier(profile, OptionalFields, "Optional", filled, missing);

        // Weighted formula
        int percentage = (int)Math.Round(
            requiredScore * 0.40 +
            encouragedScore * 0.35 +
            optionalScore * 0.25
        );
        percentage = Math.Clamp(percentage, 0, 100);

        // Next suggestion: pick highest-impact missing field
        // Priority: required > encouraged > optional, then by weight descending
        string nextSuggestion = missing
            .OrderBy(f => f.Tier == "Required" ? 0 : f.Tier == "Encouraged" ? 1 : 2)
            .ThenByDescending(f => f.Weight)
            .Select(f => f.FieldName)
            .FirstOrDefault() ?? "Your profile is complete!";

        // Map to nudge text
        var nudgeMap = RequiredFields.Concat(EncouragedFields).Concat(OptionalFields)
            .ToDictionary(f => f.Name, f => f.Nudge);
        if (nudgeMap.TryGetValue(nextSuggestion, out var nudge))
            nextSuggestion = nudge;

        return new ProfileCompletenessResult(percentage, filled, missing, nextSuggestion);
    }

    private static double EvalTier(
        UserProfile profile,
        (string Name, Func<UserProfile, bool> Check, int Weight, string Nudge)[] fields,
        string tier,
        List<ProfileFieldStatus> filled,
        List<ProfileFieldStatus> missing)
    {
        int totalWeight = fields.Sum(f => f.Weight);
        int filledWeight = 0;

        foreach (var (name, check, weight, _) in fields)
        {
            bool isFilled = check(profile);
            var status = new ProfileFieldStatus(name, isFilled, weight, tier);
            if (isFilled)
            {
                filled.Add(status);
                filledWeight += weight;
            }
            else
            {
                missing.Add(status);
            }
        }

        return totalWeight > 0 ? (double)filledWeight / totalWeight * 100 : 100;
    }

    private static int CountJsonArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return 0;
        try
        {
            var arr = JsonSerializer.Deserialize<List<string>>(json);
            return arr?.Count ?? 0;
        }
        catch { return 0; }
    }
}
