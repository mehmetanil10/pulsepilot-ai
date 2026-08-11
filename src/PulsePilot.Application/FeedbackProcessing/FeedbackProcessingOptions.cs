namespace PulsePilot.Application.FeedbackProcessing;

public sealed class FeedbackProcessingOptions
{
    public const string SectionName = "FeedbackProcessing";

    public bool Enabled { get; set; }

    public int PollIntervalMilliseconds { get; set; } = 1_000;

    public int RecoveryIntervalSeconds { get; set; } = 60;

    public int StaleProcessingThresholdMinutes { get; set; } = 5;

    public int MaxAttempts { get; set; } = 3;

    public int AnalysisTimeoutSeconds { get; set; } = 45;

    public int BaseRetryDelayMilliseconds { get; set; } = 500;

    public int MaxRetryDelaySeconds { get; set; } = 5;

    public double RetryJitterFactor { get; set; } = 0.2;

    public int MaxRecoveredPerSweep { get; set; } = 100;
}
