using PulsePilot.Domain.Feedback;

namespace PulsePilot.Application.Abstractions.Persistence;

public interface IFeedbackStatisticsRepository
{
    Task<int> CountCreatedAsync(
        Guid workspaceId,
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        CancellationToken cancellationToken = default);

    Task<FeedbackStatisticsSnapshot> GetAsync(
        Guid workspaceId,
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        CancellationToken cancellationToken = default);
}

public sealed record FeedbackStatisticsSnapshot(
    int TotalFeedbackCount,
    int AnalyzedFeedbackCount,
    decimal? AverageSeverity,
    IReadOnlyList<FeedbackStatisticCount<ProcessingStatus>> ProcessingStatuses,
    IReadOnlyList<FeedbackStatisticCount<FeedbackSource>> Sources,
    IReadOnlyList<FeedbackStatisticCount<FeedbackCategory>> Categories,
    IReadOnlyList<FeedbackStatisticCount<FeedbackComponent>> Components,
    IReadOnlyList<FeedbackStatisticCount<FeedbackSentiment>> Sentiments,
    IReadOnlyList<FeedbackSeverityCount> Severities);

public sealed record FeedbackStatisticCount<TValue>(
    TValue Value,
    int Count)
    where TValue : struct, Enum;

public sealed record FeedbackSeverityCount(
    int Severity,
    int Count);
