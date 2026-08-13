namespace PulsePilot.Application.Tools;

public sealed class ReportGenerationOptions
{
    public const string SectionName = "ReportGeneration";
    public const int MaximumAllowedPeriodDays = 365;
    public const int MaximumAllowedTrendingIssueLimit = 20;
    public const int MaximumAllowedAttempts = 5;
    public const int MaximumAllowedTimeoutSeconds = 120;
    public const int MaximumAllowedRetryDelayMilliseconds = 5_000;

    public int DefaultPeriodDays { get; set; } = 7;

    public int MaxPeriodDays { get; set; } = 90;

    public int DefaultTrendingIssueLimit { get; set; } = 5;

    public int MaxTrendingIssueLimit { get; set; } = 10;

    public int MaxAttempts { get; set; } = 2;

    public int TimeoutSeconds { get; set; } = 30;

    public int RetryDelayMilliseconds { get; set; } = 250;
}
