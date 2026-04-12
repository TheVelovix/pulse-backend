using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pulse_backend.Data;
using pulse.Models;

namespace pulse_backend.Controllers;

[ApiController]
[Route("api/projects")]
public class ProjectsController(MyDbContext db) : BaseController
{
    private readonly MyDbContext _db = db;
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetProjects()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var projects = await _db.Projects.Where(p => p.UserId == userId).ToListAsync();
        return Ok(projects);
    }

    [Authorize]
    [HttpGet("{id}")]
    public async Task<IActionResult> GetProject(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
        if (project == null) return NotFound("project-not-found");
        return Ok(project);
    }
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> CreateProject([FromBody] NewProjectBody body)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var projectsCount = await _db.Projects.CountAsync(p => p.UserId == userId);
        if (projectsCount >= 5)
        {
            return BadRequest("project-limit-reached");
        }

        var newProject = new Project
        {
            UserId = (long)userId,
            Name = body.Name,
            Domain = body.Domain,
        };
        _db.Projects.Add(newProject);
        await _db.SaveChangesAsync();
        return Ok("project-created");
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProject(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
        if (project == null) return NotFound("project-not-found");
        _db.Projects.Remove(project);
        await _db.SaveChangesAsync();
        return Ok("project-deleted");
    }
}
