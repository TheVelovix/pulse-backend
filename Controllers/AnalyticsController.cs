using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pulse.Data;
using pulse.Constants;
using pulse.Services;

namespace pulse.Controllers;

[ApiController]
[Authorize(Policy = "JwtOrApiKey")]
[Route("api/analytics")]
public class AnalyticsController(MyDbContext db, ActiveVisitorService activeVisitorService) : BaseController
{
    private readonly MyDbContext _db = db;
    private readonly ActiveVisitorService _activeVisitorService = activeVisitorService;
    private readonly HashSet<string> aiReferrers = new()
    {
        "chatgpt.com", "chat.openai.com", "perplexity.ai", "claude.ai",
        "gemini.google.com", "copilot.microsoft.com", "you.com",
        "phind.com", "poe.com", "character.ai", "mistral.ai"
    };

    [HttpGet("{id}")]
    public async Task<IActionResult> GetAnalytics(Guid id, [FromQuery] int? days, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }
        var project = await _db.Projects.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
        if (project == null)
        {
            return NotFound();
        }
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

        var views = _db.PageViews.Where(pv => pv.ProjectId == id && pv.CreatedAt >= cutoff && pv.CreatedAt <= ceiling);
        var totalViews = await views.CountAsync();
        var viewsPerDay = await views
            .GroupBy(pv => pv.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .OrderBy(x => x.Date)
            .ToListAsync();
        var topPages = await views
            .Where(pv => !pv.Url.StartsWith("outbound://"))
            .GroupBy(pv => pv.Url)
            .Select(g => new { Url = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(20)
            .ToListAsync();
        var outboundLinks = await views
            .Where(pv => pv.Url.StartsWith("outbound://"))
            .GroupBy(pv => pv.Url)
            .Select(g => new { Url = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(20)
            .ToListAsync();
        var topReferrers = await views
            .Where(pv => pv.Referrer != null)
            .GroupBy(pv => pv.Referrer)
            .Select(g => new { Referrer = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(20)
            .ToListAsync();
        var aiTraffic = await views
            .Where(pv => pv.Referrer != null && aiReferrers.Any(ai => pv.Referrer.Contains(ai)))
            .GroupBy(pv => pv.Referrer)
            .Select(g => new { Referrer = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();
        var devices = await views
            .GroupBy(pv => pv.Device)
            .Select(g => new { Device = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();
        var browsers = await views
            .GroupBy(pv => pv.Browser)
            .Select(g => new { Browser = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();
        var countries = await views
            .Where(pv => pv.Country != null)
            .GroupBy(pv => pv.Country)
            .Select(g => new { Country = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();
        var operatingSystems = await views
            .Where(pv => pv.Os != null)
            .GroupBy(pv => pv.Os)
            .Select(g => new { Os = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();
        var uniqueVisitors = await views.Select(pv => pv.SessionId).Distinct().CountAsync();
        var bounceRate = await _db.Sessions
            .Where(s => s.ProjectId == id && s.CreatedAt >= cutoff && s.CreatedAt <= ceiling)
            .Select(s => new
            {
                s.Id,
                PageViewCount = _db.PageViews.Count(pv => pv.SessionId == s.Id.ToString())
            })
            .AverageAsync(s => (double?)(s.PageViewCount == 1 ? 1.0 : 0.0)) ?? 0.0;
        var entryPages = await _db.Sessions
            .Where(s => s.ProjectId == id && s.CreatedAt >= cutoff && s.CreatedAt <= ceiling)
            .Select(s => _db.PageViews
                .Where(pv => pv.SessionId == s.Id.ToString())
                .OrderBy(pv => pv.CreatedAt)
                .Select(pv => pv.Url)
                .FirstOrDefault())
            .Where(url => url != null)
            .GroupBy(url => url)
            .Select(g => new { Url = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync();
        object? timeOnPage = null;
        if (project.User.SubscriptionPlan == Plans.Pro)
        {
            timeOnPage = await _db.Heartbeats
            .Where(h => h.ProjectId == id && h.CreatedAt >= cutoff && h.CreatedAt <= ceiling)
                .GroupBy(h => new { h.Url, h.VisitorId })
                .Select(g => new { g.Key.Url, Seconds = g.Count() * 30 })
                .GroupBy(x => x.Url)
                .Select(g => new { Url = g.Key, AvgSeconds = (int)g.Average(x => x.Seconds) })
                .OrderByDescending(x => x.AvgSeconds)
                .Take(10)
                .ToListAsync();
        }
        object? utmStats = null;
        if (project.User.SubscriptionPlan == Plans.Pro)
        {
            var utmViews = views.Where(pv => pv.UtmSource != null);
            var topSources = await utmViews
                .GroupBy(pv => pv.UtmSource)
                .Select(g => new { Source = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync();
            var topMediums = await utmViews
                   .GroupBy(pv => pv.UtmMedium)
                   .Select(g => new { Medium = g.Key, Count = g.Count() })
                   .OrderByDescending(x => x.Count)
                   .ToListAsync();

            var topCampaigns = await utmViews
                .GroupBy(pv => pv.UtmCampaign)
                .Select(g => new { Campaign = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            var topContents = await utmViews
                .Where(pv => pv.UtmContent != null)
                .GroupBy(pv => pv.UtmContent)
                .Select(g => new { Content = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            var topTerms = await utmViews
                .Where(pv => pv.UtmTerm != null)
                .GroupBy(pv => pv.UtmTerm)
                .Select(g => new { Term = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            utmStats = new { topSources, topMediums, topCampaigns, topContents, topTerms };
        }
        object? customEvents = null;
        if (project.User.SubscriptionPlan == Plans.Pro)
        {
            customEvents = await _db.CustomEvents
                .Where(e => e.ProjectId == id && e.CreatedAt >= cutoff && e.CreatedAt <= ceiling)
                .GroupBy(e => e.Name)
                .Select(g => new
                {
                    Name = g.Key,
                    Count = g.Count(),
                    TotalRevenue = g.Any(e => e.Revenue != null) ? g.Sum(e => e.Revenue) : null
                })
                .OrderByDescending(x => x.Count)
                .ToListAsync();
        }
        return Ok(new
        {
            totalViews,
            viewsPerDay,
            topPages,
            topReferrers,
            devices,
            browsers,
            countries,
            operatingSystems,
            uniqueVisitors,
            bounceRate,
            entryPages,
            timeOnPage,
            utmStats,
            outboundLinks,
            aiTraffic,
            customEvents,
        });
    }

    [HttpGet("{id}/export")]
    public async Task<IActionResult> ExportAnalytics(Guid id, [FromQuery] int? days, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }
        var project = await _db.Projects.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
        if (project == null)
        {
            return NotFound();
        }
        if (project.User.SubscriptionPlan != Plans.Pro)
        {
            return Forbid("You must be a Pro user to export analytics.");
        }
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
            cutoff = ceiling.AddDays(-days.Value);
        }
        else
        {
            cutoff = DateTime.MinValue;
        }
        if (cutoff < earliestAllowed)
        {
            cutoff = earliestAllowed;
        }

        var views = _db.PageViews.Where(pv => pv.ProjectId == id && pv.CreatedAt >= cutoff && pv.CreatedAt <= ceiling);
        var viewsPerDay = await views
            .GroupBy(pv => pv.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .OrderBy(pv => pv.Date)
            .ToListAsync();

        var topPages = await views
            .GroupBy(pv => pv.Url)
            .Select(g => new { Url = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();

        var topReferrers = await views
             .Where(pv => pv.Referrer != null)
             .GroupBy(pv => pv.Referrer)
             .Select(g => new { Referrer = g.Key, Count = g.Count() })
             .OrderByDescending(x => x.Count)
             .Take(10)
             .ToListAsync();

        var devices = await views
            .GroupBy(pv => pv.Device)
            .Select(g => new { Device = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();

        var browsers = await views
            .GroupBy(pv => pv.Browser)
            .Select(g => new { Browser = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();

        var countries = await views
            .Where(pv => pv.Country != null)
            .GroupBy(pv => pv.Country)
            .Select(g => new { Country = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();

        var sb = new System.Text.StringBuilder();

        sb.AppendLine("Views Per Day");
        sb.AppendLine("Date,Views");
        foreach (var v in viewsPerDay) sb.AppendLine($"{v.Date:yyyy-MM-dd},{v.Count}");

        sb.AppendLine();
        sb.AppendLine("Top Pages");
        sb.AppendLine("URL,Views");
        foreach (var page in topPages) sb.AppendLine($"{SanitizeCsvField(page.Url)},{page.Count}");

        sb.AppendLine();
        sb.AppendLine("Top Referrers");
        sb.AppendLine("Referrer,Views");
        foreach (var r in topReferrers) sb.AppendLine($"{SanitizeCsvField(r.Referrer)},{r.Count}");

        sb.AppendLine();
        sb.AppendLine("Devices");
        sb.AppendLine("Device,Views");
        foreach (var d in devices) sb.AppendLine($"{SanitizeCsvField(d.Device)},{d.Count}");

        sb.AppendLine();
        sb.AppendLine("Browsers");
        sb.AppendLine("Browser,Views");
        foreach (var b in browsers) sb.AppendLine($"{SanitizeCsvField(b.Browser)},{b.Count}");

        sb.AppendLine();
        sb.AppendLine("Countries");
        sb.AppendLine("Country,Views");
        foreach (var country in countries) sb.AppendLine($"{SanitizeCsvField(country.Country)},{country.Count}");

        var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"{project.Name}-analytics.csv");
    }

    private static string SanitizeCsvField(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";

        if (value.StartsWith('=') || value.StartsWith('+') || value.StartsWith('-') || value.StartsWith('@')) value = "'" + value;

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n')) value = $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
    [HttpGet("{id}/live")]
    public async Task<IActionResult> LiveVisitors(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
        if (project == null) return NotFound();

        Response.Headers["Content-Type"] = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        while (!cancellationToken.IsCancellationRequested)
        {
            var count = _activeVisitorService.GetActiveVisitors(id);
            var data = $"data: {count}\n\n";
            await Response.WriteAsync(data, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
            await Task.Delay(1000, cancellationToken);
        }

        return Ok();
    }
}
