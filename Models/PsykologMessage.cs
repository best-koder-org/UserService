namespace UserService.Models;

public enum PsykologRole { User, Assistant }

public class PsykologMessage
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public PsykologRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public PsykologSession Session { get; set; } = null!;
}
