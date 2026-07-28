namespace pulse.Models;

public class EmailVerificationCode
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(5);
    public string Email { get; set; } = string.Empty;
}
