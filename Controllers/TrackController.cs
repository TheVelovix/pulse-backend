using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using pulse.Data;
using pulse.Models;
using UAParser;
using MaxMind.GeoIP2;
using Microsoft.AspNetCore.Cors;

namespace pulse.Controllers;

[ApiController]
[Route("api/track")]
public class TrackController(MyDbContext db, DatabaseReader reader, Parser uaParser) : BaseController
{
    private readonly MyDbContext _db = db;
    private readonly DatabaseReader _reader = reader;
    private readonly Parser _uaParser = uaParser;

    [EnableCors("tracker")]
    [HttpPost]
    [EnableRateLimiting("track")]
    public async Task<IActionResult> Track(
        [FromBody] TrackBody body,
        [FromQuery(Name = "utm_source")] string? utmSource,
        [FromQuery(Name = "utm_medium")] string? utmMedium,
        [FromQuery(Name = "utm_campaign")] string? utmCampaign,
        [FromQuery(Name = "utm_content")] string? utmContent,
        [FromQuery(Name = "utm_term")] string? utmTerm
    )
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == body.ProjectId);
        if (project == null)
        {
            return NotFound("project-not-found");
        }
        var userAgent = Request.Headers.UserAgent.ToString();
        var clientInfo = _uaParser.Parse(userAgent);
        var device = clientInfo.Device.Family;
        var os = clientInfo.OS.Family;
        var browser = clientInfo.UA.Family;
        string? country = null;
        try
        {
            var ip = Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                     ?? Request.HttpContext.Connection.RemoteIpAddress?.ToString();
            if (ip != null)
            {
                var result = _reader.Country(ip);
                country = result.Country?.IsoCode;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to get country {ex.Message}");
        }
        var visitorId = body.VisitorId;
        var session = await _db.Sessions.FirstOrDefaultAsync(s => s.ProjectId == body.ProjectId && s.VisitorId == visitorId && s.LastActivity >= DateTime.UtcNow.AddMinutes(-30));
        if (session == null)
        {
            session = new Session
            {
                ProjectId = body.ProjectId,
                VisitorId = visitorId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _db.Sessions.Add(session);
            await _db.SaveChangesAsync();
        }
        else
        {
            session.LastActivity = DateTime.UtcNow;
            session.UpdatedAt = DateTime.UtcNow;
        }
        _db.PageViews.Add(new PageView
        {
            ProjectId = body.ProjectId,
            Url = body.Url,
            Referrer = body.Referrer,
            Device = device,
            Os = os,
            Country = country,
            Browser = browser,
            SessionId = session.Id.ToString(),
            UtmSource = utmSource,
            UtmMedium = utmMedium,
            UtmCampaign = utmCampaign,
            UtmContent = utmContent,
            UtmTerm = utmTerm
        });
        await _db.SaveChangesAsync();
        return Ok();
    }
}
