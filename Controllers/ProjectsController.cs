using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pulse.Constants;
using pulse.Data;
using pulse.Models;

namespace pulse.Controllers;

[ApiController]
[Authorize]
[Route("api/projects")]
public class ProjectsController(MyDbContext db) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> GetProjects()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var projects = await db.Projects.Where(p => p.UserId == userId).ToListAsync();
        return Ok(projects.Select(p => new
        {
            p.Id,
            p.Name,
            p.Domain,
            p.CreatedAt,
            p.UpdatedAt,
            p.IsPublic,
            p.PublicSlug
        }));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetProject(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
        if (project == null) return NotFound("project-not-found");
        return Ok(new
        {
            project.Id,
            project.Name,
            project.Domain,
            project.CreatedAt,
            project.UpdatedAt,
            project.IsPublic,
            project.PublicSlug
        });
    }
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

    [HttpPatch("{id}/visibility")]
    public async Task<IActionResult> ToggleProjectVisibility(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
        if (project == null) return NotFound("project-not-found");
        project.IsPublic = !project.IsPublic;
        if (project.IsPublic && string.IsNullOrEmpty(project.PublicSlug))
        {
            var baseSlug = Regex.Replace(project.Name.ToLower(), @"[^a-z0-9\s-]", "")
                                .Trim()
                                .Replace(" ", "-");
            baseSlug = Regex.Replace(baseSlug, @"-+", "-");
            if (string.IsNullOrEmpty(baseSlug)) baseSlug = id.ToString();
            var slug = baseSlug;
            var counter = 1;
            while (await db.Projects.AnyAsync(p => p.PublicSlug == slug))
            {
                slug = $"{baseSlug}-{counter++}";
            }
            project.PublicSlug = slug;
        }
        await db.SaveChangesAsync();
        return Ok(new { project.IsPublic, project.PublicSlug });
    }
}
