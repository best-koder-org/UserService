using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserService.Data;
using UserService.Models;

namespace UserService.Controllers;

/// <summary>
/// App distribution metadata + tester version reporting.
/// The Flutter app queries this on startup to report its version and check for updates.
/// </summary>
[ApiController]
[Route("api/app/version")]
public class AppVersionController : ControllerBase
{
    private static readonly HttpClient _gh = new()
    {
        Timeout = TimeSpan.FromSeconds(8),
        BaseAddress = new Uri("https://api.github.com/repos/best-koder-org/mobile_dejtingapp/"),
    };
    static AppVersionController()
    {
        _gh.DefaultRequestHeaders.UserAgent.ParseAdd("dejtingapp-backend");
    }

    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _config;

    public AppVersionController(ApplicationDbContext context, IConfiguration config)
    {
        _context = context;
        _config = config;
    }

    /// <summary>
    /// Latest published app version. Reads the newest GitHub release (tag = version,
    /// asset name encodes build code), falling back to the AppDistribution config.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetLatest()
    {
        try
        {
            using var resp = await _gh.GetAsync("releases/latest");
            if (resp.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                var root = doc.RootElement;
                var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
                var name = tag.TrimStart('v', 'V');
                var body = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";

                // Find the APK asset; parse build code from "dejtingapp-<ver>+<code>.apk"
                var versionCode = 1;
                var downloadUrl = "";
                if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                {
                    foreach (var a in assets.EnumerateArray())
                    {
                        var aname = a.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                        if (!aname.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)) continue;
                        downloadUrl = a.TryGetProperty("browser_download_url", out var u) ? u.GetString() ?? "" : downloadUrl;
                        var plus = aname.LastIndexOf('+');
                        if (plus >= 0)
                        {
                            var num = aname.Substring(plus + 1).Split('.')[0];
                            if (int.TryParse(num, out var c)) versionCode = c;
                        }
                        break;
                    }
                }

                return Ok(new { versionName = name, versionCode, downloadUrl, releaseNotes = body, updatedAt = DateTime.UtcNow });
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"AppVersionController GitHub lookup failed: {ex.Message}");
        }

        // Fallback: static AppDistribution config
        var d = _config.GetSection("AppDistribution");
        return Ok(new
        {
            versionName = d["VersionName"] ?? "1.0.0",
            versionCode = int.TryParse(d["VersionCode"], out var vc) ? vc : 1,
            downloadUrl = d["DownloadUrl"] ?? "",
            releaseNotes = d["ReleaseNotes"] ?? "",
            updatedAt = DateTime.UtcNow,
        });
    }

    /// <summary>Record which version a device is running.</summary>
    [HttpPost("report")]
    public async Task<IActionResult> Report([FromBody] AppVersionReportDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.VersionName))
            return BadRequest(new { error = "versionName is required" });

        var report = new AppVersionReport
        {
            KeycloakId = string.IsNullOrWhiteSpace(dto.KeycloakId) ? null : dto.KeycloakId,
            VersionName = dto.VersionName,
            VersionCode = dto.VersionCode,
            Platform = dto.Platform,
            DeviceModel = dto.DeviceModel,
            ReportedAt = DateTime.UtcNow,
        };
        _context.AppVersionReports.Add(report);
        await _context.SaveChangesAsync();
        return Ok(new { id = report.Id });
    }

    /// <summary>All tester version reports (dev visibility: who runs what).</summary>
    [HttpGet("reports")]
    public async Task<IActionResult> Reports([FromQuery] int limit = 100)
    {
        var rows = await _context.AppVersionReports
            .OrderByDescending(r => r.ReportedAt)
            .Take(Math.Clamp(limit, 1, 500))
            .Select(r => new
            {
                r.Id,
                r.KeycloakId,
                r.VersionName,
                r.VersionCode,
                r.Platform,
                r.DeviceModel,
                r.ReportedAt,
            })
            .ToListAsync();
        return Ok(rows);
    }
}

public class AppVersionReportDto
{
    public string? KeycloakId { get; set; }
    public string VersionName { get; set; } = string.Empty;
    public int VersionCode { get; set; }
    public string? Platform { get; set; }
    public string? DeviceModel { get; set; }
}
