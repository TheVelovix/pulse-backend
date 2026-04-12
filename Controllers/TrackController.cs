using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using pulse_backend.Data;
using pulse.Models;
using pulse_backend.Controllers;
using UAParser;
using MaxMind.GeoIP2;

namespace pulse.Controllers;

[ApiController]
[Route("api/track")]
public class TrackController(MyDbContext db, DatabaseReader reader, Parser uaParser) : BaseController
{
    private readonly MyDbContext _db = db;
    private readonly DatabaseReader _reader = reader;
    private readonly Parser _uaParser = uaParser;

    [HttpPost]
    [EnableRateLimiting("track")]
    public async Task<IActionResult> Track([FromBody] TrackBody body)
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
            var ip = Request.HttpContext.Connection.RemoteIpAddress?.ToString();
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
        _db.PageViews.Add(new PageView
        {
            ProjectId = body.ProjectId,
            Url = body.Url,
            Referrer = body.Referrer,
            Device = device,
            Os = os,
            Country = country,
            Browser = browser
        });
        await _db.SaveChangesAsync();
        return Ok();
    }
}
