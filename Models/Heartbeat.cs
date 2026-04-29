namespace pulse.Models;

public class Heartbeat
{
    public long Id { get; set; }
    public Guid ProjectId { get; set; }
    public string VisitorId { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Project Project { get; set; } = null!;
}
