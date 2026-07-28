namespace pulse.Models;

public class PreSignUpBody
{
    public string Email { get; set; } = string.Empty;
    public string TurnstileToken { get; set; } = string.Empty;
}
