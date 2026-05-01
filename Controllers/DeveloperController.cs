using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pulse.Data;
using pulse.Constants;
using pulse.Services;
using pulse.Helpers;
using pulse.Models;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;

namespace pulse.Controllers;

[ApiController]
[Authorize(Policy = "ApiKey")]
[Route("api/v1")]
public class DeveloperController(MyDbContext db, ActiveVisitorService activeVisitorService, Utils utils, PaddleService paddleService) : BaseController
{
    private readonly MyDbContext _db = db;
    private readonly ActiveVisitorService _activeVisitorService = activeVisitorService;
    private readonly Utils _utils = utils;
    private readonly PaddleService _paddleService = paddleService;

    [HttpGet("analytics/{id}")]
    public async Task<IActionResult> GetAnalytics(Guid id, int? days, DateTime? from, DateTime? to)
    {
        var analytics = await _utils.GetProjectAnalytics(id, GetUserId(), days, from, to);
        if (analytics == null) return NotFound("project-not-found");
        return Ok(analytics);
    }
    [HttpGet("analytics/{id}/export")]
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

    [HttpGet("analytics/{id}/live")]
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

    [HttpGet("projects")]
    public async Task<IActionResult> GetProjects()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var projects = await _db.Projects.Where(p => p.UserId == userId).ToListAsync();
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
    [HttpGet("projects/{id}")]
    public async Task<IActionResult> GetProject(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
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
    [HttpPost("projects")]
    public async Task<IActionResult> CreateProject([FromBody] NewProjectBody body)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var user = await _db.Users.FindAsync(userId);
        if (user == null) return Unauthorized();
        var projectLimit = Plans.ProjectLimits[user.SubscriptionPlan];
        var projectsCount = await _db.Projects.CountAsync(p => p.UserId == userId);
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
        _db.Projects.Add(newProject);
        await _db.SaveChangesAsync();
        return Ok("project-created");
    }
    [HttpDelete("projects/{id}")]
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


    [HttpPatch("projects/{id}/visibility")]
    public async Task<IActionResult> ToggleProjectVisibility(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
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
            while (await _db.Projects.AnyAsync(p => p.PublicSlug == slug))
            {
                slug = $"{baseSlug}-{counter++}";
            }
            project.PublicSlug = slug;
        }
        await _db.SaveChangesAsync();
        return Ok(new { project.IsPublic, project.PublicSlug });
    }

    [HttpDelete("user/delete-account")]
    public async Task<IActionResult> DeleteAccount()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var user = await _db.Users.FindAsync(userId);
        if (user == null) return Unauthorized();

        // Cancel paddle subscription if on pro
        if (user.SubscriptionPlan == "pro" && user.PaddleSubscriptionId != null)
            await _paddleService.CancelSubscription(user.PaddleSubscriptionId, true);

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();

        return Ok();
    }

    [HttpGet("user/apiKeys")]
    public async Task<IActionResult> CreateApiKey()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return Unauthorized();
        if (user.SubscriptionPlan != Plans.Pro) return Unauthorized("api-key-requires-pro-plan");

        var apiKeys = await _db.ApiKeys.Where(k => k.UserId == userId.Value).ToListAsync();
        var strippedKeys = apiKeys.Select(k => new { name = k.Name, createdAt = k.CreatedAt });
        return Ok(strippedKeys);
    }
    [HttpPost("user/apiKeys")]
    public async Task<IActionResult> CreateApiKey([FromQuery] string name)
    {
        if (string.IsNullOrEmpty(name)) return BadRequest("api-key-name-required");
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return Unauthorized();
        if (user.SubscriptionPlan != Plans.Pro) return Unauthorized("api-key-requires-pro-plan");
        var existingKey = await _db.ApiKeys.FirstOrDefaultAsync(k => k.Name == name && k.UserId == userId);
        if (existingKey != null)
        {
            return BadRequest("api-key-name-taken");
        }

        var randomByes = RandomNumberGenerator.GetBytes(64);
        var rawKey = $"pulse_live_{Convert.ToHexString(randomByes).ToLower()}";
        var hashedKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey))).ToLower();
        var apiKey = new ApiKey
        {
            UserId = userId.Value,
            HashedKey = hashedKey,
            Name = name
        };

        _db.ApiKeys.Add(apiKey);
        await _db.SaveChangesAsync();

        return Ok(new { key = rawKey });
    }
    [HttpDelete("user/apiKeys")]
    public async Task<IActionResult> DeleteApiKey([FromQuery] string name)
    {
        if (string.IsNullOrEmpty(name)) return BadRequest("api-key-name-required");
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return Unauthorized();
        if (user.SubscriptionPlan != Plans.Pro) return Unauthorized("api-key-requires-pro-plan");
        var apiKey = await _db.ApiKeys.FirstOrDefaultAsync(k => k.Name == name && k.UserId == userId.Value);
        if (apiKey == null) return NotFound();
        _db.ApiKeys.Remove(apiKey);
        await _db.SaveChangesAsync();
        return Ok();
    }
}
