namespace UserService.Models;

/// <summary>
/// Records which app version a device/tester is running.
/// Reported by the Flutter app on startup (see AppUpdateService).
/// </summary>
public class AppVersionReport
{
    public int Id { get; set; }

    /// <summary>Keycloak user ID (who the tester is), when logged in.</summary>
    public string? KeycloakId { get; set; }

    public string VersionName { get; set; } = string.Empty;
    public int VersionCode { get; set; }
    public string? Platform { get; set; }
    public string? DeviceModel { get; set; }
    public DateTime ReportedAt { get; set; } = DateTime.UtcNow;
}
