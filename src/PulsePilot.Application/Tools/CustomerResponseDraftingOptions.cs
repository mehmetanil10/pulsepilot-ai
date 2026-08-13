namespace PulsePilot.Application.Tools;

public sealed class CustomerResponseDraftingOptions
{
    public const string SectionName = "CustomerResponseDrafting";
    public const int MaximumAllowedAttempts = 5;
    public const int MaximumAllowedTimeoutSeconds = 120;
    public const int MaximumAllowedRetryDelayMilliseconds = 5_000;

    public int MaxAttempts { get; set; } = 2;

    public int TimeoutSeconds { get; set; } = 30;

    public int RetryDelayMilliseconds { get; set; } = 250;
}
