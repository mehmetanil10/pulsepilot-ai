namespace PulsePilot.Infrastructure.Persistence.Seeding;

public sealed class DemoSeedOptions
{
    public const string SectionName = "Seed";
    public const int MinimumFeedbackCount = 100;
    public const int MaximumFeedbackCount = 10_000;

    public bool Run { get; init; }

    public string Email { get; init; } = "demo@pulsepilot.ai";

    public string Password { get; init; } = string.Empty;

    public string DisplayName { get; init; } = "PulsePilot Demo Owner";

    public string WorkspaceName { get; init; } = "PulsePilot Demo";

    public int FeedbackCount { get; init; } = MinimumFeedbackCount;
}
