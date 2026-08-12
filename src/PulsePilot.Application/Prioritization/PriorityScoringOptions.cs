namespace PulsePilot.Application.Prioritization;

public sealed class PriorityScoringOptions
{
    public const string SectionName = "PriorityScoring";

    public decimal SeverityWeight { get; set; } = 0.35m;

    public decimal FrequencyWeight { get; set; } = 0.30m;

    public decimal CustomerImpactWeight { get; set; } = 0.20m;

    public decimal RecencyWeight { get; set; } = 0.15m;

    public int FrequencyNormalizationCount { get; set; } = 20;

    public int CustomerImpactNormalizationCount { get; set; } = 10;

    public int RecencyWindowDays { get; set; } = 7;

    public decimal P1Threshold { get; set; } = 75m;

    public decimal P2Threshold { get; set; } = 50m;

    public decimal P3Threshold { get; set; } = 25m;
}
