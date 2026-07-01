namespace pulse.Models;

public class EmailChangeCode
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public long UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
