namespace pulse.Models;

public class ProjectWeeklyReport
{
    public string ProjectName { get; set; } = string.Empty;
    public int TotalViewsThisWeek { get; set; }
    public int TotalViewsLastWeek { get; set; }
    public int PercentChange => TotalViewsLastWeek == 0
        ? 100
        : (int)Math.Round((TotalViewsThisWeek - TotalViewsLastWeek) / (double)TotalViewsLastWeek * 100);
    public List<PageStat> TopPages { get; set; } = [];
    public List<ReferrerStat> TopReferrers { get; set; } = [];
}

public class PageStat
{
    public string Url { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class ReferrerStat
{
    public string? Referrer { get; set; }
    public int Count { get; set; }
}
