using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using pulse.Data;
using pulse.Services;
using pulse.Models;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using pulse.Constants;

namespace pulse.Controllers;

[ApiController]
[Authorize]
[Route("api/user")]
public class UserController(MyDbContext db, PaddleService paddleService) : BaseController
{
    [HttpDelete("delete-account")]
    public async Task<IActionResult> DeleteAccount()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var user = await db.Users.FindAsync(userId);
        if (user == null) return Unauthorized();

        // Cancel paddle subscription if on pro
        if (user.SubscriptionPlan == "pro" && user.PaddleSubscriptionId != null)
            await paddleService.CancelSubscription(user.PaddleSubscriptionId, true);

        db.Users.Remove(user);
        await db.SaveChangesAsync();

        return Ok();
    }

    [HttpGet("apiKeys")]
    public async Task<IActionResult> CreateApiKey()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return Unauthorized();
        if (user.SubscriptionPlan != Plans.Pro) return Unauthorized("api-key-requires-pro-plan");

        var apiKeys = await db.ApiKeys.Where(k => k.UserId == userId.Value).ToListAsync();
        var strippedKeys = apiKeys.Select(k => new { name = k.Name, createdAt = k.CreatedAt });
        return Ok(strippedKeys);
    }
    [HttpPost("apiKeys")]
    public async Task<IActionResult> CreateApiKey([FromQuery] string name)
    {
        if (string.IsNullOrEmpty(name)) return BadRequest("api-key-name-required");
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return Unauthorized();
        if (user.SubscriptionPlan != Plans.Pro) return Unauthorized("api-key-requires-pro-plan");
        var existingKey = await db.ApiKeys.FirstOrDefaultAsync(k => k.Name == name && k.UserId == userId);
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

        db.ApiKeys.Add(apiKey);
        await db.SaveChangesAsync();

        return Ok(new { key = rawKey });
    }
    [HttpDelete("apiKeys")]
    public async Task<IActionResult> DeleteApiKey([FromQuery] string name)
    {
        if (string.IsNullOrEmpty(name)) return BadRequest("api-key-name-required");
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return Unauthorized();
        if (user.SubscriptionPlan != Plans.Pro) return Unauthorized("api-key-requires-pro-plan");
        var apiKey = await db.ApiKeys.FirstOrDefaultAsync(k => k.Name == name && k.UserId == userId.Value);
        if (apiKey == null) return NotFound();
        db.ApiKeys.Remove(apiKey);
        await db.SaveChangesAsync();
        return Ok();
    }
}
