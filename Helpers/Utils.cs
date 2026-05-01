using pulse.Constants;
using pulse.Data;
using Microsoft.EntityFrameworkCore;
using pulse.Models;
using Microsoft.AspNetCore.Mvc;
namespace pulse.Helpers;

public class Utils(MyDbContext db)
{
    private readonly MyDbContext _db = db;
    private readonly HashSet<string> aiReferrers = new()
    {
        "chatgpt.com", "chat.openai.com", "perplexity.ai", "claude.ai",
        "gemini.google.com", "copilot.microsoft.com", "you.com",
        "phind.com", "poe.com", "character.ai", "mistral.ai"
    };

    public async Task<AnalyticsResult?> GetProjectAnalytics(Guid projectId, long? userId, int? days, DateTime? from, DateTime? to)
    {
        Project? project;
        if (userId.HasValue)
        {
            project = await _db.Projects.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == projectId && p.UserId == userId);
        }
        else
        {
            project = await _db.Projects.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == projectId);
        }
        if (project == null) return null;
        var maxRetentionDays = Plans.RetentionDays[project.User.SubscriptionPlan];
        var earliestAllowed = DateTime.UtcNow.AddDays(-maxRetentionDays);
        DateTime cutoff;
        DateTime ceiling = to.HasValue ? to.Value.ToUniversalTime() : DateTime.UtcNow;
        if (from.HasValue)
        {
            cutoff = from.Value.ToUniversalTime();
        }
        else if (days.HasValue)
        {
            cutoff = DateTime.UtcNow.AddDays(-days.Value);
        }
        else
        {
            cutoff = DateTime.MinValue;
        }
        if (cutoff < earliestAllowed) cutoff = earliestAllowed;

        var views = _db.PageViews.Where(pv => pv.ProjectId == projectId && pv.CreatedAt >= cutoff && pv.CreatedAt <= ceiling);
        var totalViews = await views.CountAsync();
        var viewsPerDay = await views
            .GroupBy(pv => pv.CreatedAt.Date)
            .Select(g => new DailyView { Date = g.Key, Count = g.Count() })
            .OrderBy(x => x.Date)
            .ToListAsync();
        var topPages = await views
            .Where(pv => !pv.Url.StartsWith("outbound://"))
            .GroupBy(pv => pv.Url)
            .Select(g => new Page { Url = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(20)
            .ToListAsync();
        var outboundLinks = await views
            .Where(pv => pv.Url.StartsWith("outbound://"))
            .GroupBy(pv => pv.Url)
            .Select(g => new OutboundLink { Url = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(20)
            .ToListAsync();
        var topReferrers = await views
            .Where(pv => pv.Referrer != null)
            .GroupBy(pv => pv.Referrer)
            .Select(g => new Referrers { Referrer = g.Key!, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(20)
            .ToListAsync();
        var aiTraffic = await views
            .Where(pv => pv.Referrer != null && aiReferrers.Any(ai => pv.Referrer.Contains(ai)))
            .GroupBy(pv => pv.Referrer)
            .Select(g => new Referrers { Referrer = g.Key!, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();
        var devices = await views
            .GroupBy(pv => pv.Device)
            .Select(g => new Devices { Device = g.Key!, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();
        var browsers = await views
            .GroupBy(pv => pv.Browser)
            .Select(g => new Browsers { Browser = g.Key!, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();
        var countries = await views
            .Where(pv => pv.Country != null)
            .GroupBy(pv => pv.Country)
            .Select(g => new Countries { Country = g.Key!, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();
        var operatingSystems = await views
            .Where(pv => pv.Os != null)
            .GroupBy(pv => pv.Os)
            .Select(g => new OperatingSystem { Os = g.Key!, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();
        var uniqueVisitors = await views.Select(pv => pv.SessionId).Distinct().CountAsync();
        var bounceRate = await _db.Sessions
            .Where(s => s.ProjectId == projectId && s.CreatedAt >= cutoff && s.CreatedAt <= ceiling)
            .Select(s => new
            {
                s.Id,
                PageViewCount = _db.PageViews.Count(pv => pv.SessionId == s.Id.ToString())
            })
            .AverageAsync(s => (double?)(s.PageViewCount == 1 ? 1.0 : 0.0)) ?? 0.0;
        var entryPages = await _db.Sessions
            .Where(s => s.ProjectId == projectId && s.CreatedAt >= cutoff && s.CreatedAt <= ceiling)
            .Select(s => _db.PageViews
                .Where(pv => pv.SessionId == s.Id.ToString())
                .OrderBy(pv => pv.CreatedAt)
                .Select(pv => pv.Url)
                .FirstOrDefault())
            .Where(url => url != null)
            .GroupBy(url => url)
            .Select(g => new Page { Url = g.Key!, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync();
        List<TimeOnPage>? timeOnPage = null;
        if (project.User.SubscriptionPlan == Plans.Pro)
        {
            timeOnPage = await _db.Heartbeats
            .Where(h => h.ProjectId == projectId && h.CreatedAt >= cutoff && h.CreatedAt <= ceiling)
                .GroupBy(h => new { h.Url, h.VisitorId })
                .Select(g => new { g.Key.Url, Seconds = g.Count() * 30 })
                .GroupBy(x => x.Url)
                .Select(g => new TimeOnPage { Url = g.Key, AvgSeconds = (int)g.Average(x => x.Seconds) })
                .OrderByDescending(x => x.AvgSeconds)
                .Take(10)
                .ToListAsync();
        }
        Utm? utmStats = null;
        if (project.User.SubscriptionPlan == Plans.Pro)
        {
            var utmViews = views.Where(pv => pv.UtmSource != null);
            var topSources = await utmViews
                .GroupBy(pv => pv.UtmSource)
                .Select(g => new TopSource { Source = g.Key!, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync();
            var topMediums = await utmViews
                   .GroupBy(pv => pv.UtmMedium)
                   .Select(g => new TopMedium { Medium = g.Key!, Count = g.Count() })
                   .OrderByDescending(x => x.Count)
                   .ToListAsync();

            var topCampaigns = await utmViews
                .GroupBy(pv => pv.UtmCampaign)
                .Select(g => new TopCampaign { Campaign = g.Key!, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            var topContents = await utmViews
                .Where(pv => pv.UtmContent != null)
                .GroupBy(pv => pv.UtmContent)
                .Select(g => new TopContent { Content = g.Key!, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            var topTerms = await utmViews
                .Where(pv => pv.UtmTerm != null)
                .GroupBy(pv => pv.UtmTerm)
                .Select(g => new TopTerm { Term = g.Key!, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            utmStats = new Utm
            {
                TopSources = topSources,
                TopMediums = topMediums,
                TopCampaigns = topCampaigns,
                TopContents = topContents,
                TopTerms = topTerms
            };
        }
        List<CustomEvent>? customEvents = null;
        if (project.User.SubscriptionPlan == Plans.Pro)
        {
            customEvents = await _db.CustomEvents
                .Where(e => e.ProjectId == projectId && e.CreatedAt >= cutoff && e.CreatedAt <= ceiling)
                .GroupBy(e => e.Name)
                .Select(g => new
                CustomEvent
                {
                    Name = g.Key,
                    Count = g.Count(),
                    TotalRevenue = g.Any(e => e.Revenue != null) ? g.Sum(e => e.Revenue) : null
                })
                .OrderByDescending(x => x.Count)
                .ToListAsync();
        }
        return new AnalyticsResult
        {
            TotalViews = totalViews,
            ViewsPerDay = viewsPerDay,
            TopPages = topPages,
            OutboundLinks = outboundLinks,
            TopReferrers = topReferrers,
            AiTraffic = aiTraffic,
            Devices = devices,
            Browsers = browsers,
            Countries = countries,
            OperatingSystems = operatingSystems,
            UniqueVisitors = uniqueVisitors,
            BounceRate = bounceRate,
            EntryPages = entryPages,
            TimeOnPage = timeOnPage,
            UtmStats = utmStats,
            CustomEvents = customEvents?.Cast<CustomEvent>().ToList() ?? []
        };
    }

    public async Task<byte[]> ExportCsv(AnalyticsResult analytics)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var prop in analytics.GetType().GetProperties())
        {
            var value = prop.GetValue(analytics);
            var propertyName = GetPropertyNameForCsv(prop.Name);
            sb.AppendLine(propertyName);
            if (value == null) continue;
            switch (prop.Name)
            {
                case nameof(AnalyticsResult.TotalViews):
                    sb.AppendLine(value.ToString());
                    break;
                case nameof(AnalyticsResult.ViewsPerDay):
                    sb.AppendLine("Date,Views");
                    foreach (var v in (List<DailyView>)value) sb.AppendLine($"{v.Date:yyyy-MM-dd},{v.Count}");
                    break;
                case nameof(AnalyticsResult.TopPages):
                    sb.AppendLine("URL,Views");
                    foreach (var p in (List<Page>)value) sb.AppendLine($"{SanitizeCsvField(p.Url)},{p.Count}");
                    break;
                case nameof(AnalyticsResult.OutboundLinks):
                    sb.AppendLine("URL,Count");
                    foreach (var l in (List<OutboundLink>)value) sb.AppendLine($"{SanitizeCsvField(l.Url)},{l.Count}");
                    break;
                case nameof(AnalyticsResult.TopReferrers):
                    sb.AppendLine("Referrer,Views");
                    foreach (var r in (List<Referrers>)value) sb.AppendLine($"{SanitizeCsvField(r.Referrer)},{r.Count}");
                    break;
                case nameof(AnalyticsResult.AiTraffic):
                    sb.AppendLine("AI Referrer,Views");
                    foreach (var r in (List<Referrers>)value) sb.AppendLine($"{SanitizeCsvField(r.Referrer)},{r.Count}");
                    break;
                case nameof(AnalyticsResult.Devices):
                    sb.AppendLine("Device,Views");
                    foreach (var d in (List<Devices>)value) sb.AppendLine($"{SanitizeCsvField(d.Device)},{d.Count}");
                    break;
                case nameof(AnalyticsResult.Browsers):
                    sb.AppendLine("Browser,Views");
                    foreach (var b in (List<Browsers>)value) sb.AppendLine($"{SanitizeCsvField(b.Browser)},{b.Count}");
                    break;
                case nameof(AnalyticsResult.Countries):
                    sb.AppendLine("Country,Views");
                    foreach (var c in (List<Countries>)value) sb.AppendLine($"{SanitizeCsvField(c.Country)},{c.Count}");
                    break;
                case nameof(AnalyticsResult.OperatingSystems):
                    sb.AppendLine("Operating System,Views");
                    foreach (var os in (List<OperatingSystem>)value) sb.AppendLine($"{SanitizeCsvField(os.Os)},{os.Count}");
                    break;
                case nameof(AnalyticsResult.UniqueVisitors):
                    sb.AppendLine(value.ToString());
                    break;
                case nameof(AnalyticsResult.BounceRate):
                    string percentage = $"{(double)value * 100:F2}%";
                    sb.AppendLine(percentage);
                    break;
                case nameof(AnalyticsResult.EntryPages):
                    sb.AppendLine("Page,Views");
                    foreach (var p in (List<Page>)value) sb.AppendLine($"{SanitizeCsvField(p.Url)},{p.Count}");
                    break;
                case nameof(AnalyticsResult.TimeOnPage):
                    sb.AppendLine("Page, Avg. Seconds");
                    foreach (var t in (List<TimeOnPage>)value) sb.AppendLine($"{SanitizeCsvField(t.Url)},{t.AvgSeconds}");
                    break;
                case nameof(AnalyticsResult.CustomEvents):
                    sb.AppendLine("Event,Times Triggered,Total Revenue");
                    foreach (var e in (List<CustomEvent>)value) sb.AppendLine($"{e.Name},{e.Count},{e.TotalRevenue ?? 0}");
                    break;
                case nameof(AnalyticsResult.UtmStats):
                    var utm = (Utm)value;
                    foreach (var property in utm.GetType().GetProperties())
                    {
                        var primitiveName = nameof(property);
                        var utmPropName = GetUtmTypeName(primitiveName);
                        var utmValue = property.GetValue(utm);
                        switch (primitiveName)
                        {
                            case nameof(Utm.TopSources):
                                sb.AppendLine("Source,Count");
                                if (utmValue == null) break;
                                foreach (var c in (List<TopSource>)utmValue) sb.AppendLine($"{SanitizeCsvField(c.Source)},{c.Count}");
                                break;
                            case nameof(Utm.TopMediums):
                                sb.AppendLine("Medium,Count");
                                if (utmValue == null) break;
                                foreach (var m in (List<TopMedium>)utmValue) sb.AppendLine($"{SanitizeCsvField(m.Medium)},{m.Count}");
                                break;
                            case nameof(Utm.TopCampaigns):
                                sb.AppendLine("Campaign,Count");
                                if (utmValue == null) break;
                                foreach (var c in (List<TopCampaign>)utmValue) sb.AppendLine($"{SanitizeCsvField(c.Campaign)},{c.Count}");
                                break;
                            case nameof(Utm.TopContents):
                                sb.AppendLine("Content,Count");
                                if (utmValue == null) break;
                                foreach (var c in (List<TopContent>)utmValue) sb.AppendLine($"{SanitizeCsvField(c.Content)},{c.Count}");
                                break;
                            case nameof(Utm.TopTerms):
                                sb.AppendLine("Term,Count");
                                if (utmValue == null) break;
                                foreach (var t in (List<TopTerm>)utmValue) sb.AppendLine($"{SanitizeCsvField(t.Term)},{t.Count}");
                                break;
                        }
                    }
                    break;
            }
            sb.AppendLine("");
        }
        var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        return bytes;
    }
    private static string GetPropertyNameForCsv(string propertyName)
    {
        switch (propertyName)
        {
            case nameof(AnalyticsResult.TotalViews):
                return "\"Total Views\"";
            case nameof(AnalyticsResult.ViewsPerDay):
                return "\"Views Per Day\"";
            case nameof(AnalyticsResult.TopPages):
                return "\"Top Pages\"";
            case nameof(AnalyticsResult.OutboundLinks):
                return "\"Outbound Links\"";
            case nameof(AnalyticsResult.TopReferrers):
                return "\"Top Referrers\"";
            case nameof(AnalyticsResult.AiTraffic):
                return "\"AI Traffic\"";
            case nameof(AnalyticsResult.Devices):
            case nameof(AnalyticsResult.Browsers):
            case nameof(AnalyticsResult.Countries):
                return propertyName;
            case nameof(AnalyticsResult.OperatingSystems):
                return "\"Operating Systems\"";
            case nameof(AnalyticsResult.UniqueVisitors):
                return "\"Unique Visitors\"";
            case nameof(AnalyticsResult.BounceRate):
                return "\"Bounce Rate\"";
            case nameof(AnalyticsResult.EntryPages):
                return "\"Entry Pages\"";
            case nameof(AnalyticsResult.TimeOnPage):
                return "\"Time on Page\"";
            case nameof(AnalyticsResult.UtmStats):
                return "\"UTM Stats\"";
            case nameof(AnalyticsResult.CustomEvents):
                return "\"Custom Events\"";
            default:
                return "";
        }
    }
    private static string GetUtmTypeName(string propertyName)
    {
        switch (propertyName)
        {
            case nameof(Utm.TopSources):
                return "Top Sources";
            case nameof(Utm.TopMediums):
                return "Top Mediums";
            case nameof(Utm.TopCampaigns):
                return "Top Campaigns";
            case nameof(Utm.TopContents):
                return "Top Contents";
            case nameof(Utm.TopTerms):
                return "Top Terms";
            default:
                return "";
        }
    }
    private static string SanitizeCsvField(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";

        if (value.StartsWith('=') || value.StartsWith('+') || value.StartsWith('-') || value.StartsWith('@')) value = "'" + value;

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n')) value = $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}

public class AnalyticsResult
{
    public int TotalViews { get; set; }
    public List<DailyView> ViewsPerDay { get; set; } = [];
    public List<Page> TopPages { get; set; } = [];
    public List<OutboundLink> OutboundLinks { get; set; } = [];
    public List<Referrers> TopReferrers { get; set; } = [];
    public List<Referrers> AiTraffic { get; set; } = [];
    public List<Devices> Devices { get; set; } = [];
    public List<Browsers> Browsers { get; set; } = [];
    public List<Countries> Countries { get; set; } = [];
    public List<OperatingSystem> OperatingSystems { get; set; } = [];
    public int UniqueVisitors { get; set; }
    public double BounceRate { get; set; }
    public List<Page> EntryPages { get; set; } = [];
    public List<TimeOnPage>? TimeOnPage { get; set; } = [];
    public Utm? UtmStats { get; set; } = new Utm();
    public List<CustomEvent> CustomEvents { get; set; } = [];
}
public class DailyView
{
    public DateTime Date { get; set; }
    public int Count { get; set; }
}
public class Page
{
    public string Url { get; set; } = string.Empty;
    public int Count { get; set; }
}
public class OutboundLink : Page;
public class Referrers
{
    public string Referrer { get; set; } = string.Empty;
    public int Count { get; set; }
}
public class Devices
{
    public string Device { get; set; } = string.Empty;
    public int Count { get; set; }
}
public class Browsers
{
    public string Browser { get; set; } = string.Empty;
    public int Count { get; set; }
}
public class Countries
{
    public string Country { get; set; } = string.Empty;
    public int Count { get; set; }
}
public class OperatingSystem
{
    public string Os { get; set; } = string.Empty;
    public int Count { get; set; }
}
public class TimeOnPage
{
    public string Url { get; set; } = string.Empty;
    public int AvgSeconds { get; set; }
}
public class Utm
{
    public List<TopSource> TopSources { get; set; } = [];
    public List<TopMedium> TopMediums { get; set; } = [];
    public List<TopCampaign> TopCampaigns { get; set; } = [];
    public List<TopContent> TopContents { get; set; } = [];
    public List<TopTerm> TopTerms { get; set; } = [];
}
public class TopSource
{
    public string Source { get; set; } = string.Empty;
    public int Count { get; set; }
}
public class TopMedium
{
    public string Medium { get; set; } = string.Empty;
    public int Count { get; set; }
}
public class TopCampaign
{
    public string Campaign { get; set; } = string.Empty;
    public int Count { get; set; }
}
public class TopContent
{
    public string Content { get; set; } = string.Empty;
    public int Count { get; set; }
}
public class TopTerm
{
    public string Term { get; set; } = string.Empty;
    public int Count { get; set; }
}
public class CustomEvent
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal? TotalRevenue { get; set; }
}
