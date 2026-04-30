using Microsoft.AspNetCore.Mvc;
using pulse.Data;
using Microsoft.EntityFrameworkCore;
using pulse.Helpers;

namespace pulse.Controllers;

[ApiController]
[Route("api/public")]
public class PublicDataController(MyDbContext db, Utils utils) : BaseController
{
    private readonly MyDbContext _db = db;
    private readonly Utils _utils = utils;

    [HttpGet("{slug}")]
    public async Task<IActionResult> GetProjectAnalytics(string slug)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.PublicSlug == slug && p.IsPublic);

        if (project == null)
        {
            return NotFound();
        }
        var analytics = await _utils.GetProjectAnalytics(project.Id, null, null, null, null);

        return Ok(analytics);
    }
}
