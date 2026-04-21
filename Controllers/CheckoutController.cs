using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pulse.Data;
using pulse.Services;

namespace pulse.Controllers;

[ApiController]
[Route("api/checkout")]
[Authorize]
public class CheckoutController(MyDbContext db, PaddleService paddleService) : BaseController
{
    private readonly MyDbContext _db = db;
    private readonly PaddleService _paddleService = paddleService;

    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        Console.WriteLine($"Checkout User ID: {userId}");
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return Unauthorized();

        if (user.SubscriptionPlan == "pro") return BadRequest("already subscribed");

        var checkoutUrl = await _paddleService.CreateCheckoutTransaction(user.Email, user.Id);
        return Ok(new { url = checkoutUrl });
    }

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var user = await _db.Users.FindAsync(userId);
        if (user == null) return Unauthorized();

        if (user.SubscriptionPlan != "pro") return BadRequest("not subscribed");

        var success = await _paddleService.CancelSubscription(user.PaddleSubscriptionId!);
        if (!success) return StatusCode(500, "failed to cancel subscription");

        return Ok();
    }
}
