namespace pulse.Models;

public class TrackBody
{
    public Guid ProjectId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? Referrer { get; set; }
    public string VisitorId { get; set; } = string.Empty;
}
