namespace PulsePilot.Application.Prioritization;

public sealed record PriorityScoringMember(
    Guid FeedbackId,
    string? CustomerName,
    string? CustomerEmail,
    DateTimeOffset CreatedAt,
    int? Severity);
