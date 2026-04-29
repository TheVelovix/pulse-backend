using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Cors;
using pulse.Data;
using pulse.Models;
using pulse.Constants;
using System.Text.Json;


namespace pulse.Controllers;

[ApiController]
[Route("api/events")]
public class EventsController(MyDbContext db) : BaseController
{
    private readonly MyDbContext _db = db;


    [EnableCors("tracker")]
    [HttpPost]
    [EnableRateLimiting("track")]
    public async Task<IActionResult> TrackEvent([FromBody] EventBody body)
    {
        var project = await _db.Projects.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == body.ProjectId);
        if (project == null) return NotFound("project-not-found");
        if (project.User.SubscriptionPlan != Plans.Pro) return Forbid("Custom events are a Pro Feature.");

        var session = await _db.Sessions
            .Where(s => s.ProjectId == body.ProjectId && s.VisitorId == body.VisitorId && s.LastActivity >= DateTime.UtcNow.AddMinutes(-30))
            .OrderByDescending(s => s.LastActivity)
            .FirstOrDefaultAsync();

        _db.CustomEvents.Add(new CustomEvent
        {
            ProjectId = body.ProjectId,
            Name = body.Name,
            Url = body.Url,
            VisitorId = body.VisitorId,
            SessionId = session?.Id.ToString(),
            Revenue = body.Revenue,
            Props = body.Props != null ? JsonSerializer.Serialize(body.Props) : null,
        });

        await _db.SaveChangesAsync();
        return Ok();
    }
}

public class EventBody
{
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string VisitorId { get; set; } = string.Empty;
    public decimal? Revenue { get; set; }
    public Dictionary<string, string>? Props { get; set; }
}
