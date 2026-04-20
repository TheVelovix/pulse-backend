using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pulse.Constants;
using pulse.Data;
using pulse.Models;

namespace pulse.Controllers;

[ApiController]
[Route("api/projects")]
public class ProjectsController(MyDbContext db) : BaseController
{
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetProjects()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var projects = await db.Projects.Where(p => p.UserId == userId).ToListAsync();
        return Ok(projects);
    }

    [Authorize]
    [HttpGet("{id}")]
    public async Task<IActionResult> GetProject(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
        if (project == null) return NotFound("project-not-found");
        return Ok(project);
    }
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> CreateProject([FromBody] NewProjectBody body)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var user = await db.Users.FindAsync(userId);
        if (user == null) return Unauthorized();
        var projectLimit = Plans.ProjectLimits[user.SubscriptionPlan];
        var projectsCount = await db.Projects.CountAsync(p => p.UserId == userId);
        if (projectsCount >= projectLimit)
        {
            return BadRequest("project-limit-reached");
        }

        var newProject = new Project
        {
            UserId = (long)userId,
            Name = body.Name,
            Domain = body.Domain,
        };
        db.Projects.Add(newProject);
        await db.SaveChangesAsync();
        return Ok("project-created");
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProject(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
        if (project == null) return NotFound("project-not-found");
        db.Projects.Remove(project);
        await db.SaveChangesAsync();
        return Ok("project-deleted");
    }
}
