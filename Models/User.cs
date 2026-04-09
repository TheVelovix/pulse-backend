namespace pulse.Models;

public class User
{
    public long Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Project> Projects { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
