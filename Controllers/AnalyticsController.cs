using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pulse.Data;
using pulse.Constants;
using pulse.Services;
using pulse.Helpers;

namespace pulse.Controllers;

[ApiController]
[Authorize]
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
        var analytics = await _utils.GetProjectAnalytics(project.Id, userId, days, from, to);
        if (analytics == null)
        {
            return StatusCode(500, "Failed to export csv");
        }
        var csvBytes = await _utils.ExportCsv(analytics);
        return File(csvBytes, "text/csv", $"{project.Name}-analytics.csv");
    }
    [HttpGet("{id}/live")]
    public async Task<IActionResult> LiveVisitors(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
        if (project == null) return NotFound();

        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
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
