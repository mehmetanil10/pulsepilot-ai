namespace PulsePilot.Application.Tools;

public sealed class TrendingIssuesOptions
{
    public const string SectionName = "TrendingIssues";
    public const int DefaultPeriod = 7;
    public const int MaximumAllowedPeriod = 365;
    public const int DefaultResultLimit = 10;
    public const int MaximumResultLimit = 50;

    public int DefaultPeriodDays { get; set; } = DefaultPeriod;

    public int MaxPeriodDays { get; set; } = MaximumAllowedPeriod;

    public int DefaultLimit { get; set; } = DefaultResultLimit;

    public int MaxLimit { get; set; } = MaximumResultLimit;
}
