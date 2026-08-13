using PulsePilot.Domain.Feedback;

namespace PulsePilot.Application.Tools;

public interface IGetFeedbackStatisticsTool
{
    Task<GetFeedbackStatisticsToolResult> ExecuteAsync(
        Guid workspaceId,
        GetFeedbackStatisticsToolInput input,
        CancellationToken cancellationToken = default);
}

public sealed record GetFeedbackStatisticsToolInput(
    int? PeriodDays = null);

public sealed record GetFeedbackStatisticsToolResult(
    DateTimeOffset FromInclusive,
    DateTimeOffset ToExclusive,
    int PeriodDays,
    int TotalFeedbackCount,
    int AnalyzedFeedbackCount,
    decimal? AverageSeverity,
    IReadOnlyList<ProcessingStatusStatistic> ProcessingStatuses,
    IReadOnlyList<FeedbackSourceStatistic> Sources,
    IReadOnlyList<FeedbackCategoryStatistic> Categories,
    IReadOnlyList<FeedbackComponentStatistic> Components,
    IReadOnlyList<FeedbackSentimentStatistic> Sentiments,
    IReadOnlyList<FeedbackSeverityStatistic> Severities);

public sealed record ProcessingStatusStatistic(
    ProcessingStatus Status,
    int Count);

public sealed record FeedbackSourceStatistic(
    FeedbackSource Source,
    int Count);

public sealed record FeedbackCategoryStatistic(
    FeedbackCategory Category,
    int Count);

public sealed record FeedbackComponentStatistic(
    FeedbackComponent Component,
    int Count);

public sealed record FeedbackSentimentStatistic(
    FeedbackSentiment Sentiment,
    int Count);

public sealed record FeedbackSeverityStatistic(
    int Severity,
    int Count);
