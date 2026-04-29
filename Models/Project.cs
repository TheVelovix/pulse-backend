namespace pulse.Models;

public class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
    public ICollection<PageView> PageViews { get; set; } = [];
    public ICollection<Session> Sessions { get; set; } = [];
    public ICollection<Heartbeat> Heartbeats { get; set; } = [];
    public ICollection<CustomEvent> CustomEvents { get; set; } = [];
}
