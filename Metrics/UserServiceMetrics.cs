using System.Diagnostics.Metrics;

namespace UserService.Metrics;

/// <summary>
/// Injectable business metrics for UserService.
/// Registered as singleton, instruments are created once and reused.
/// </summary>
public sealed class UserServiceMetrics
{
    public const string MeterName = "UserService";

    private readonly Counter<long> _profilesCreated;
    private readonly Counter<long> _profilesUpdated;
    private readonly Counter<long> _profilesDeleted;
    private readonly Histogram<double> _searchDuration;

    public UserServiceMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _profilesCreated = meter.CreateCounter<long>(
            "user_profiles_created_total",
            description: "Total number of user profiles created");
        _profilesUpdated = meter.CreateCounter<long>(
            "user_profiles_updated_total",
            description: "Total number of user profiles updated");
        _profilesDeleted = meter.CreateCounter<long>(
            "user_profiles_deleted_total",
            description: "Total number of user profiles deleted");
        _searchDuration = meter.CreateHistogram<double>(
            "user_search_duration_ms",
            unit: "ms",
            description: "Duration of user profile search queries in milliseconds");
    }

    public void ProfileCreated() => _profilesCreated.Add(1);
    public void ProfileUpdated() => _profilesUpdated.Add(1);
    public void ProfileDeleted() => _profilesDeleted.Add(1);
    public void RecordSearchDuration(double ms) => _searchDuration.Record(ms);
}
