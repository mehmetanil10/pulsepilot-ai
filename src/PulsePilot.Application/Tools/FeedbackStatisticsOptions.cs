namespace PulsePilot.Application.Tools;

public sealed class FeedbackStatisticsOptions
{
    public const string SectionName = "FeedbackStatistics";
    public const int DefaultPeriod = 7;
    public const int MaximumAllowedPeriod = 365;

    public int DefaultPeriodDays { get; set; } = DefaultPeriod;

    public int MaxPeriodDays { get; set; } = MaximumAllowedPeriod;
}
