using Microsoft.AspNetCore.Mvc;
using pulse.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using pulse_backend.Services;
using pulse.Models;

namespace pulse.Controllers;

[ApiController]
[Route("api/refresh")]
public class RefreshController(MyDbContext db, JwtService jwtService) : ControllerBase
{
    private readonly MyDbContext _db = db;
    private readonly JwtService _jwtService = jwtService;
    private readonly bool _isProduction = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production";

    [HttpPost]
    public async Task<IActionResult> NewRefreshToken()
    {
        var refreshToken = Request.Cookies["refreshToken"];
        if (refreshToken == null) return Unauthorized("invalid-refresh-token");
        ClaimsPrincipal principal;
        var jwtHandler = new JwtSecurityTokenHandler();
        try
        {
            principal = jwtHandler.ValidateToken(refreshToken, _jwtService.TokenValidationParams, out _);
        }
        catch (SecurityTokenException)
        {
            return Unauthorized("invalid-refresh-token");
        }
        try
        {
            Console.WriteLine($"NAME IDENTIFIER: {principal.FindFirst(ClaimTypes.NameIdentifier)?.Value}");
            var userId = long.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            var dbRefreshToken = await _db.RefreshTokens.Include(t => t.User).FirstOrDefaultAsync(t => t.Token == refreshToken && t.UserId == userId);
            if (dbRefreshToken == null) return Unauthorized("invalid-refresh-token");
            var newTokens = _jwtService.GenerateTokens(dbRefreshToken.User);
            Response.Cookies.Append("accessToken", newTokens.AccessToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Domain = _isProduction ? ".velovix.com" : null,
                Expires = DateTime.UtcNow.AddHours(1)
            });
            Response.Cookies.Append("refreshToken", newTokens.RefreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Domain = _isProduction ? ".velovix.com" : null,
                Expires = DateTime.UtcNow.AddDays(7)
            });
            await _db.RefreshTokens.Where(t => t.Token == refreshToken).ExecuteDeleteAsync();
            _db.RefreshTokens.Add(new RefreshToken
            {
                UserId = userId,
                Token = newTokens.RefreshToken,
            });
            await _db.SaveChangesAsync();
            return Ok("success");
        }
        catch (Exception)
        {
            return BadRequest("invalid-refresh-token");
        }

    }
}
