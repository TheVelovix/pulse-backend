using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using pulse.Services;
using pulse.Constants;
using pulse.Data;

namespace pulse.Controllers;

[ApiController]
[Route("api/payments")]
public class WebhookController(MyDbContext db, PaddleService paddleService) : BaseController
{
    private readonly MyDbContext _db = db;
    private readonly PaddleService _paddleService = paddleService;

    [HttpPost]
    public async Task<IActionResult> HandleWebhook()
    {
        var signature = Request.Headers["Paddle-Signature"].ToString();
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync();
        if (!IsValidSignature(rawBody, signature))
        {
            return Unauthorized();
        }

        var payload = JsonDocument.Parse(rawBody).RootElement;
        var eventType = payload.GetProperty("event_type").GetString();
        var data = payload.GetProperty("data");

        var userIdStr = data
            .GetProperty("custom_data")
            .GetProperty("user_id")
            .GetString();

        if (!long.TryParse(userIdStr, out var userId))
        {
            return BadRequest();
        }

        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound();

        switch (eventType)
        {
            case "subscription.activated":
            case "subscription.created":
            case "subscription.resumed":
                user.SubscriptionPlan = Plans.Pro;
                user.PaddleSubscriptionId = data.GetProperty("id").GetString()!;
                break;
            case "subscription.canceled":
                user.SubscriptionPlan = Plans.Free;
                user.PaddleSubscriptionId = null;
                break;
            case "subscription.past_due":
            case "subscription.paused":
                user.SubscriptionPlan = Plans.Free;
                break;
            case "subscription.updated":
                var status = data.GetProperty("status").GetString();
                user.SubscriptionPlan = status == "active" ? Plans.Pro : Plans.Free;
                break;
        }
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok();
    }

    private bool IsValidSignature(string rawBody, string signature)
    {
        var parts = signature.Split(';');
        var ts = parts[0].Replace("ts=", "");
        var h1 = parts[1].Replace("h1=", "");

        var signed = $"{ts}:{rawBody}";
        var keyBytes = Encoding.UTF8.GetBytes(_paddleService._webhookSecret);
        var msgBytes = Encoding.UTF8.GetBytes(signed);

        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(msgBytes);
        var computed = Convert.ToHexString(hash).ToLower();

        return computed == h1;
    }
}
