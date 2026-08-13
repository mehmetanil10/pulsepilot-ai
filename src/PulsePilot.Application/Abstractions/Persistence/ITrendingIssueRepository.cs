using PulsePilot.Domain.Feedback;

namespace PulsePilot.Application.Abstractions.Persistence;

public interface ITrendingIssueRepository
{
    Task<IReadOnlyList<TrendingIssueSnapshot>> ListAsync(
        Guid workspaceId,
        DateTimeOffset previousFromInclusive,
        DateTimeOffset currentFromInclusive,
        DateTimeOffset currentToExclusive,
        int limit,
        CancellationToken cancellationToken = default);
}

public sealed record TrendingIssueSnapshot(
    Guid FeedbackClusterId,
    string Title,
    FeedbackCategory Category,
    FeedbackComponent Component,
    FeedbackPriority Priority,
    decimal PriorityScore,
    int CurrentPeriodCount,
    int PreviousPeriodCount);
