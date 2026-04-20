using System.ComponentModel.DataAnnotations;

namespace pulse.Models;

public class SignUpBody
{
    public string Email { get; set; } = string.Empty;
    [Required]
    [MinLength(8)]
    [RegularExpression(@"^(?=.*[A-Z])(?=.*[^a-zA-Z0-9]).+$",
        ErrorMessage = "Password must contain at least one uppercase letter and one symbol")]
    public string Password { get; set; } = string.Empty;
    public string TurnstileToken { get; set; } = string.Empty;
}
