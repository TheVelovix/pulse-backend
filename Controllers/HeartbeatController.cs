
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using pulse.Services;

namespace pulse.Controllers;

[ApiController]
[Route("api/heartbeat")]
[EnableCors("tracker")]
public class HeartbeatController(ActiveVisitorService activeVisitorService) : BaseController
{
    private readonly ActiveVisitorService _activeVisitorService = activeVisitorService;

    [HttpPost]
    public IActionResult RecordHeartbeat([FromBody] HeartbeatDto dto)
    {
        _activeVisitorService.RecordHeartBeat(dto.ProjectId, dto.VisitorId);
        return Ok();
    }

}

public class HeartbeatDto
{
    public Guid ProjectId { get; set; }
    public string VisitorId { get; set; } = string.Empty;
}
