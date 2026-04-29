namespace pulse.Models;

public class CustomEvent
{
    public long Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? VisitorId { get; set; }
    public string? SessionId { get; set; }
    public decimal? Revenue { get; set; }
    public string? Props { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Project Project { get; set; } = null!;
}
