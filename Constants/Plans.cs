namespace pulse.Constants;

public static class Plans
{
    public const string Free = "free";
    public const string Pro = "pro";

    public static readonly Dictionary<string, int> RetentionDays = new()
    {
        { Free, 30 },
        { Pro, 365 },
    };

    public static readonly Dictionary<string, int> ProjectLimits = new()
    {
        { Free, 5 },
        { Pro,  int.MaxValue },
    };
}
