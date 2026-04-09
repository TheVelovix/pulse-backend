using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pulse_backend.Data;
using pulse.Models;
using pulse_backend.Services;

namespace pulse_backend.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(JwtService jwtService, MyDbContext db) : ControllerBase
{
    private readonly JwtService _jwtService = jwtService;
    private readonly MyDbContext _db = db;
    private readonly EmailAddressAttribute _email = new();

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
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(body.Password);
        var newUser = new User
        {
            Email = body.Email,
            Password = hashedPassword
        };
        await _db.Users.AddAsync(newUser);
        await _db.SaveChangesAsync();
        var tokens = _jwtService.GenerateTokens(newUser);
        Response.Cookies.Append("accessToken", tokens.AccessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddHours(1)
        });
        Response.Cookies.Append("refreshToken", tokens.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
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
        var tokens = _jwtService.GenerateTokens(user);
        Response.Cookies.Append("accessToken", tokens.AccessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddHours(1)
        });
        Response.Cookies.Append("refreshToken", tokens.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddDays(7)
        });
        return Ok("success");
    }
}
