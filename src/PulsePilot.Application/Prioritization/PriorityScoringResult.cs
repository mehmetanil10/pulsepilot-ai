using PulsePilot.Domain.Feedback;

namespace PulsePilot.Application.Prioritization;

public sealed record PriorityScoringResult(
    decimal Score,
    FeedbackPriority Priority,
    decimal SeverityFactor,
    decimal FrequencyFactor,
    decimal CustomerImpactFactor,
    decimal RecencyFactor);
