using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pulse.Data;
using pulse.Models;
using pulse_backend.Services;
using Microsoft.AspNetCore.Authorization;
using pulse.Services;
using pulse.Constants;
using System.Security.Cryptography;
using Microsoft.AspNetCore.RateLimiting;

namespace pulse.Controllers;

[ApiController]
[EnableRateLimiting("auth")]
[Route("api/auth")]
public class AuthController(JwtService jwtService, MyDbContext db, TurnstileService turnstile, EmailService emailService) : BaseController
{
    private readonly JwtService _jwtService = jwtService;
    private readonly MyDbContext _db = db;
    private readonly EmailAddressAttribute _email = new();
    private readonly bool _isProduction = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production";
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
        bool passedTurnstile = await _turnstile.VerifyTurnstile(body.TurnstileToken);
        if (!passedTurnstile)
        {
            return BadRequest("captcha-failed");
        }
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(body.Password);
        var newUser = new User
        {
            Email = body.Email,
            Password = hashedPassword
        };
        await _db.Users.AddAsync(newUser);
        await _db.SaveChangesAsync();
        _ = _emailService.SendAsync(newUser.Email, "Welcome to Pulse", EmailTemplates.Welcome(newUser.Email));
        var tokens = _jwtService.GenerateTokens(newUser);
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
        return Ok(new { user.Id, user.Email, user.SubscriptionPlan });
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
    public async Task<IActionResult> ResetPassword([FromQuery] string code, [FromQuery] string newPassword)
    {
        var token = await _db.PasswordResetTokens.FirstOrDefaultAsync(t => t.Token == code);
        if (token == null || token.ExpiresAt < DateTime.UtcNow) return BadRequest("invalid-code");
        var user = await _db.Users.FindAsync(token.UserId);
        if (user == null) return NotFound();
        user.Password = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _db.SaveChangesAsync();
        await _db.PasswordResetTokens.Where(t => t.UserId == user.Id).ExecuteDeleteAsync();

        return Ok();
    }
}
