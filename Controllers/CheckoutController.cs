using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pulse.Constants;
using pulse.Data;
using pulse.Services;
using pulse.Models;

namespace pulse.Controllers;

[ApiController]
[Route("api/checkout")]
[Authorize]
public class CheckoutController(MyDbContext db, PaddleService paddleService) : BaseController
{
    private readonly MyDbContext _db = db;
    private readonly PaddleService _paddleService = paddleService;

    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromQuery] bool annual)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return Unauthorized();
        bool alreadySubscribed = user.SubscriptionPlan == Plans.Pro || (user.BundledSubscription != null && user.BundledSubscription.ExpiresAt > DateTime.UtcNow);
        if (alreadySubscribed) return BadRequest("already subscribed");
        var checkoutUrl = await _paddleService.CreateCheckoutTransaction(user.Email, user.Id, annual);
        return Ok(new { url = checkoutUrl });
    }

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var user = await _db.Users.FindAsync(userId);
        if (user == null) return Unauthorized();

        if (user.BundledSubscription != null && user.BundledSubscription.ExpiresAt > DateTime.UtcNow) return BadRequest("cannot-cancel-bundled-subscription");
        if (user.SubscriptionPlan != Plans.Pro) return BadRequest("not subscribed");

        var success = await _paddleService.CancelSubscription(user.PaddleSubscriptionId!);
        if (!success) return StatusCode(500, "failed to cancel subscription");

        return Ok();
    }
    [HttpPost("activateCode")]
    public async Task<IActionResult> ActivatePromoCode([FromQuery] string promoCode)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var user = await _db.Users.FindAsync(userId);
        if (user == null) return Unauthorized();
        var code = await _db.BusinessPromotionalCodes.FirstOrDefaultAsync(c => c.Code == promoCode);
        if (code == null || code.Used) return BadRequest("invalid-code");
        if (user.SubscriptionPlan == Plans.Pro || (user.BundledSubscription != null && user.BundledSubscription.ExpiresAt > DateTime.UtcNow)) return BadRequest("already-subscribed");
        user.BundledSubscription = new BundledSubscription
        {
            Plan = Plans.Pro,
            ExpiresAt = DateTime.UtcNow.AddDays(code.Duration)
        };
        code.Used = true;
        await _db.SaveChangesAsync();
        return Ok();
    }
}
