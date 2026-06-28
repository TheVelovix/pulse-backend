namespace pulse.Models;

public class BusinessPromotionalCode
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Plan { get; set; } = string.Empty;
    // How long the user will get the plan for when the code is used
    public int Duration { get; set; }
    public bool Used { get; set; } = false;
}
