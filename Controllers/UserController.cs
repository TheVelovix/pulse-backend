using Microsoft.AspNetCore.Mvc;
using pulse.Data;
using pulse.Services;

namespace pulse.Controllers;

[ApiController]
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
}
