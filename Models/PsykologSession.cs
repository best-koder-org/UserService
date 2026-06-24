namespace UserService.Models;

public enum PsykologSessionStatus { Active, Completed, Expired }

public class PsykologSession
{
    public int Id { get; set; }
    public string KeycloakId { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public int ThemeCount { get; set; }
    public PsykologSessionStatus Status { get; set; } = PsykologSessionStatus.Active;
    public int SessionNumber { get; set; }

    public List<PsykologMessage> Messages { get; set; } = [];
}
