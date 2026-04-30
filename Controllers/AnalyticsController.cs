using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pulse.Data;
using pulse.Constants;
using pulse.Services;
using pulse.Helpers;

namespace pulse.Controllers;

[ApiController]
[Authorize(Policy = "JwtOrApiKey")]
[Route("api/analytics")]
public class AnalyticsController(MyDbContext db, ActiveVisitorService activeVisitorService, Utils utils) : BaseController
{
    private readonly MyDbContext _db = db;
    private readonly ActiveVisitorService _activeVisitorService = activeVisitorService;
    private readonly Utils _utils = utils;

    [HttpGet("{id}")]
    public async Task<IActionResult> GetAnalytics(Guid id, [FromQuery] int? days, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }
        var analytics = await _utils.GetProjectAnalytics(id, userId.Value, days, from, to);
        if (analytics == null) return NotFound("project-not-found");
        return Ok(analytics);
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
