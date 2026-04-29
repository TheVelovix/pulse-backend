using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pulse.Constants;
using pulse.Data;
using pulse.Services;
using pulse.Models;

namespace pulse.Controllers;

[ApiController]
[Route("api/heartbeat")]
[EnableCors("tracker")]
public class HeartbeatController(ActiveVisitorService activeVisitorService, MyDbContext db) : BaseController
{
    private readonly ActiveVisitorService _activeVisitorService = activeVisitorService;
    private readonly MyDbContext _db = db;

    [HttpPost]
    public async Task<IActionResult> RecordHeartbeat([FromBody] HeartbeatDto dto)
    {
        var project = await _db.Projects.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == dto.ProjectId);
        if (project == null) return NotFound();
        if (project.User.SubscriptionPlan != Plans.Pro) return BadRequest();
        _activeVisitorService.RecordHeartBeat(dto.ProjectId, dto.VisitorId);
        _db.Heartbeats.Add(new Heartbeat
        {
            ProjectId = dto.ProjectId,
            VisitorId = dto.VisitorId,
            Url = dto.Url,
        });
        await _db.SaveChangesAsync();
        return Ok();
    }

}

public class HeartbeatDto
{
    public Guid ProjectId { get; set; }
    public string VisitorId { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}
