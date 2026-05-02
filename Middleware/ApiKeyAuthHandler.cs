using System.Security.Cryptography;
using System.Text;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using pulse.Data;
using pulse.Constants;
namespace pulse.Middleware;

public class ApiKeyAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    MyDbContext db

) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Api-Key", out var rawKey))
        {
            rawKey = Request.Query["api_key"];
            if (string.IsNullOrEmpty(rawKey))
                return AuthenticateResult.NoResult();
        }

        var hashedKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey!))).ToLower();

        var apiKey = await db.ApiKeys
            .Include(k => k.User)
            .FirstOrDefaultAsync(k => k.HashedKey == hashedKey);

        if (apiKey == null)
            return AuthenticateResult.Fail("Invalid API key");

        if (apiKey.User.SubscriptionPlan != Plans.Pro)
        {
            return AuthenticateResult.Fail("Dev API is a Pro Feature");
        }
        _ = Task.Run(async () =>
        {
            using var scope = Request.HttpContext.RequestServices.CreateScope();
            var scopedDb = scope.ServiceProvider.GetRequiredService<MyDbContext>();
            await scopedDb.ApiKeys
                .Where(k => k.Id == apiKey.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(k => k.LastUsed, DateTime.UtcNow));
            await scopedDb.SaveChangesAsync();
        });

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, apiKey.UserId.ToString()),
            new Claim("api_key_id", apiKey.Id.ToString()),
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}
