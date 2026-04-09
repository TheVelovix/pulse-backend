using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using pulse_backend.Data;
using pulse.Models;

namespace pulse_backend.Services;

public class JwtService(MyDbContext db)
{
    private readonly string _secret = Environment.GetEnvironmentVariable("JWT_SECRET")!;
    private readonly MyDbContext _db = db;
    public TokenValidationParameters TokenValidationParams => new()
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("JWT_SECRET")!)),
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER"),
        ValidAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE")
    };
    public Tokens GenerateTokens(User user)
    {
        var claims = new[] {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            claims: claims,
            signingCredentials: credentials,
            issuer: Environment.GetEnvironmentVariable("JWT_ISSUER"),
            audience: Environment.GetEnvironmentVariable("JWT_AUDIENCE"),
            expires: DateTime.UtcNow.AddHours(1)
        );
        var reToken = new JwtSecurityToken(

            claims: claims,
            signingCredentials: credentials,
            issuer: Environment.GetEnvironmentVariable("JWT_ISSUER"),
            audience: Environment.GetEnvironmentVariable("JWT_AUDIENCE"),
            expires: DateTime.UtcNow.AddDays(7)
        );
        string accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        string refreshToken = new JwtSecurityTokenHandler().WriteToken(reToken);
        _db.RefreshTokens.Add(new RefreshToken { UserId = user.Id, Token = refreshToken });
        _db.SaveChanges();
        return new Tokens { AccessToken = accessToken, RefreshToken = refreshToken };
    }
}
public class Tokens
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}
