using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pulse_backend.Controllers;
using pulse.Data;

namespace pulse.Controllers;

[ApiController]
[Route("api/projects")]
public class AnalyticsController(MyDbContext db) : BaseController
{
    private readonly MyDbContext _db = db;

    [Authorize]
    [HttpGet("{id}/analytics")]
    public async Task<IActionResult> GetAnalytics(Guid id, [FromQuery] int? days)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
        if (project == null)
        {
            return NotFound();
        }
        var cutoff = days.HasValue ? DateTime.UtcNow.AddDays(-days.Value) : DateTime.MinValue;
        var views = _db.PageViews.Where(pv => pv.ProjectId == id && pv.CreatedAt >= cutoff);
        var totalViews = await views.CountAsync();
        var viewsPerDay = await views
            .GroupBy(pv => pv.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .OrderBy(x => x.Date)
            .ToListAsync();
        var topPages = await views
            .GroupBy(pv => pv.Url)
            .Select(g => new { Url = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
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

        return Ok(new
        {
            totalViews,
            viewsPerDay,
            topPages,
            topReferrers,
            devices,
            browsers,
            countries,
        });
    }
}
