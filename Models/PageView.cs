namespace pulse.Models;

public class PageView
{
    public long Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? Referrer { get; set; }
    public string? Device { get; set; }
    public string? Os { get; set; }
    public string? Country { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Project Project { get; set; } = null!;
}
