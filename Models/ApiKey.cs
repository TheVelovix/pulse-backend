namespace pulse.Models;

public class ApiKey
{
    public Guid Id { get; set; }
    public long UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string HashedKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsed { get; set; }
    public User User { get; set; } = null!;
}
