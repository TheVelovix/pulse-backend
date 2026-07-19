using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pulse.Data;
using pulse.Models;
using pulse.Services;
using Microsoft.AspNetCore.Authorization;
using pulse.Constants;
using System.Security.Cryptography;
using Microsoft.AspNetCore.RateLimiting;

namespace pulse.Controllers;

[ApiController]
[EnableRateLimiting("auth")]
[Route("api/auth")]
public class AuthController(JwtService jwtService, MyDbContext db, TurnstileService turnstile, EmailService emailService, IWebHostEnvironment env) : BaseController
{
    private readonly JwtService _jwtService = jwtService;
    private readonly MyDbContext _db = db;
    private readonly EmailAddressAttribute _email = new();
    private readonly bool _isProduction = env.IsProduction();
    private readonly TurnstileService _turnstile = turnstile;
    private readonly EmailService _emailService = emailService;

    [HttpPost("signup")]
    public async Task<IActionResult> Signup([FromBody] SignUpBody body)
    {
        bool isValidEmail = _email.IsValid(body.Email);
        if (!isValidEmail)
        {
            return BadRequest("invalid-email");
        }
        var existingUser = await _db.Users.FirstOrDefaultAsync(u => u.Email == body.Email);
        if (existingUser != null)
        {
            return BadRequest("user-already-exists");
        }
        var deviceType = Request.Headers["X-Device-Type"].ToString();
        if(string.IsNullOrWhiteSpace(deviceType) || deviceType != "mobile"){
            bool passedTurnstile = await _turnstile.VerifyTurnstile(body.TurnstileToken);
            Console.WriteLine(passedTurnstile);
            if (!passedTurnstile)
            {
                return BadRequest("captcha-failed");
            }
        }
        BundledSubscription? bundledSubscription = null;
        if (body.PromotionalCode != null && !string.IsNullOrWhiteSpace(body.PromotionalCode))
        {
            var code = await _db.BusinessPromotionalCodes.FirstOrDefaultAsync(c => c.Code == body.PromotionalCode);
            if (code == null || code.Used) return BadRequest("invalid-promotional-code");
            bundledSubscription = new BundledSubscription
            {
                Plan = code.Plan,
                ExpiresAt = DateTime.UtcNow.AddDays(code.Duration)
            };
        }
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(body.Password);
        var newUser = new User
        {
            Email = body.Email,
            Password = hashedPassword,
            BundledSubscription = bundledSubscription
        };
        await _db.Users.AddAsync(newUser);
        await _db.SaveChangesAsync();
        _ = _emailService.SendAsync(newUser.Email, "Welcome to Pulse", EmailTemplates.Welcome(newUser.Email));
        var tokens = _jwtService.GenerateTokens(newUser);
        if(deviceType == "mobile"){
            return Ok(new
            {
                accessToken = tokens.AccessToken,
                refreshToken = tokens.RefreshToken
            });
        }
        Response.Cookies.Append("accessToken", tokens.AccessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Domain = _isProduction ? ".velovix.com" : null,
            Expires = DateTime.UtcNow.AddHours(1)
        });
        Response.Cookies.Append("refreshToken", tokens.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Domain = _isProduction ? ".velovix.com" : null,
            Expires = DateTime.UtcNow.AddDays(7)
        });
        return Ok("success");
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginBody body)
    {
        var isValidEmail = _email.IsValid(body.Email);
        if (!isValidEmail)
        {
            return BadRequest("invalid-credentials");
        }
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == body.Email);
        if (user == null)
        {
            return BadRequest("invalid-credentials");
        }
        var isPasswordValid = BCrypt.Net.BCrypt.Verify(body.Password, user.Password);
        if (!isPasswordValid)
        {
            return BadRequest("invalid-credentials");
        }
        var deviceType = Request.Headers["X-Device-Type"].ToString();
        if(deviceType == "mobile"){
            var jwtTokens = _jwtService.GenerateTokens(user);
            return Ok(new
            {
                accessToken = jwtTokens.AccessToken,
                refreshToken = jwtTokens.RefreshToken
            });
        }
        bool passedTurnstile = await _turnstile.VerifyTurnstile(body.TurnstileToken);
        if (!passedTurnstile)
        {
            return BadRequest("captcha-failed");
        }
        var tokens = _jwtService.GenerateTokens(user);
        Response.Cookies.Append("accessToken", tokens.AccessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Domain = _isProduction ? ".velovix.com" : null,
            Expires = DateTime.UtcNow.AddMinutes(20)
        });
        Response.Cookies.Append("refreshToken", tokens.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Domain = _isProduction ? ".velovix.com" : null,
            Expires = DateTime.UtcNow.AddDays(7)
        });
        return Ok("success");
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = GetUserId();
        var user = await _db.Users.FindAsync(userId);
        if (user == null)
        {
            return NotFound();
        }
        string subscriptionPlan;
        if (user.BundledSubscription != null && user.BundledSubscription.ExpiresAt > DateTime.UtcNow)
        {
            subscriptionPlan = user.BundledSubscription.Plan;
        }
        else subscriptionPlan = user.SubscriptionPlan;
        return Ok(new { user.Id, user.Email, subscriptionPlan });
    }

    [Authorize]
    [HttpDelete("logout")]
    public async Task<IActionResult> Logout()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        await _db.RefreshTokens.Where(t => t.UserId == userId).ExecuteDeleteAsync();
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Domain = _isProduction ? ".velovix.com" : null,
            Expires = DateTime.UtcNow.AddDays(-1)
        };

        Response.Cookies.Append("accessToken", "", cookieOptions);
        Response.Cookies.Append("refreshToken", "", cookieOptions);
        return Ok("success");
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromQuery] string email)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null) return Ok();
        await _db.PasswordResetTokens.Where(t => t.UserId == user.Id).ExecuteDeleteAsync();
        var code = RandomNumberGenerator.GetInt32(0, 1000000).ToString("D6");
        var token = new PasswordResetToken
        {
            UserId = user.Id,
            Token = code,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15)
        };
        _db.PasswordResetTokens.Add(token);
        await _db.SaveChangesAsync();
        _ = _emailService.SendAsync(email, "Reset your password", EmailTemplates.PasswordResetEmail(code));

        return Ok();
    }

    [HttpPatch("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest body)
    {
        var token = await _db.PasswordResetTokens.FirstOrDefaultAsync(t => t.Token == body.Code);
        if (token == null || token.ExpiresAt < DateTime.UtcNow) return BadRequest("invalid-code");
        var user = await _db.Users.FindAsync(token.UserId);
        if (user == null) return NotFound();
        user.Password = BCrypt.Net.BCrypt.HashPassword(body.NewPassword);
        await _db.SaveChangesAsync();
        await _db.PasswordResetTokens.Where(t => t.UserId == user.Id).ExecuteDeleteAsync();

        return Ok();
    }

    char[] letters = ['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9'];

    [Authorize]
    [HttpPatch("requestEmailChange")]
    public async Task<IActionResult> RequestEmailChange([FromBody] EmailChangeBody body)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        // Check if new email is in use
        var user = await _db.Users.Where(u => u.Email == body.Email).FirstOrDefaultAsync();
        if (user != null) return BadRequest("email-in-use");
        var randomCode = "";
        var random = new Random();
        for (int i = 0; i < 6; i++)
        {
            var randomIndex = random.Next(0, letters.Length);
            randomCode += letters[randomIndex];
        }

        var newCodeEntry = new EmailChangeCode
        {
            Code = randomCode,
            UserId = (long)userId,
            Email = body.Email,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        };
        _db.EmailChangeCodes.Add(newCodeEntry);
        await _db.SaveChangesAsync();
        await _emailService.SendAsync(body.Email, "Email Change Request", EmailTemplates.EmailChangeCodeEmail(randomCode, body.Email));
        return Ok();
    }

    [Authorize]
    [HttpPatch("confirmEmailChange")]
    public async Task<IActionResult> ConfirmEmailChange([FromQuery] string code)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var dbCode = await _db.EmailChangeCodes.Where(c => c.Code == code && c.UserId == userId).FirstOrDefaultAsync();
        if (dbCode == null) return BadRequest("invalid-code");
        if (dbCode.ExpiresAt < DateTime.UtcNow) return BadRequest("code-expired");
        var user = await _db.Users.Where(u => u.Id == userId).FirstOrDefaultAsync();
        if (user == null) return Unauthorized();
        user.Email = dbCode.Email;
        await _db.SaveChangesAsync();
        return Ok();
    }

    [Authorize]
    [HttpDelete("logOutOtherDevices")]
    public async Task<IActionResult> LogOutOtherDevices()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var currentRefreshToken = Request.Cookies["refreshToken"];
        if (currentRefreshToken == null) return Unauthorized();
        await _db.RefreshTokens.Where(t => t.UserId == userId && t.Token != currentRefreshToken).ExecuteDeleteAsync();
        return Ok();
    }
}
public class EmailChangeBody
{
    public string Email { get; set; } = string.Empty;
}
public class ResetPasswordRequest
{
    public string Code { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
