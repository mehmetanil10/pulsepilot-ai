using Microsoft.Extensions.Options;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Domain.Feedback;

namespace PulsePilot.Application.Tools;

internal sealed class GetFeedbackStatisticsTool(
    IFeedbackStatisticsRepository feedbackStatisticsRepository,
    IOptions<FeedbackStatisticsOptions> feedbackStatisticsOptions,
    TimeProvider timeProvider) : IGetFeedbackStatisticsTool
{
    private readonly FeedbackStatisticsOptions _options = feedbackStatisticsOptions.Value;

    public async Task<GetFeedbackStatisticsToolResult> ExecuteAsync(
        Guid workspaceId,
        GetFeedbackStatisticsToolInput input,
        CancellationToken cancellationToken = default)
    {
        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("Workspace id is required.", nameof(workspaceId));
        }

        ArgumentNullException.ThrowIfNull(input);
        var periodDays = input.PeriodDays ?? _options.DefaultPeriodDays;

        if (periodDays is < 1 || periodDays > _options.MaxPeriodDays)
        {
            throw new ArgumentOutOfRangeException(
                nameof(input),
                $"Feedback statistics period must be between 1 and {_options.MaxPeriodDays} days.");
        }

        var toExclusive = timeProvider.GetUtcNow().ToUniversalTime();
        var fromInclusive = toExclusive.AddDays(-periodDays);
        var snapshot = await feedbackStatisticsRepository.GetAsync(
            workspaceId,
            fromInclusive,
            toExclusive,
            cancellationToken);

        return new GetFeedbackStatisticsToolResult(
            fromInclusive,
            toExclusive,
            periodDays,
            snapshot.TotalFeedbackCount,
            snapshot.AnalyzedFeedbackCount,
            snapshot.AverageSeverity,
            CompleteBreakdown(
                snapshot.ProcessingStatuses,
                (value, count) => new ProcessingStatusStatistic(value, count)),
            CompleteBreakdown(
                snapshot.Sources,
                (value, count) => new FeedbackSourceStatistic(value, count)),
            CompleteBreakdown(
                snapshot.Categories,
                (value, count) => new FeedbackCategoryStatistic(value, count)),
            CompleteBreakdown(
                snapshot.Components,
                (value, count) => new FeedbackComponentStatistic(value, count)),
            CompleteBreakdown(
                snapshot.Sentiments,
                (value, count) => new FeedbackSentimentStatistic(value, count)),
            CompleteSeverityBreakdown(snapshot.Severities));
    }

    private static IReadOnlyList<TStatistic> CompleteBreakdown<TValue, TStatistic>(
        IReadOnlyList<FeedbackStatisticCount<TValue>> counts,
        Func<TValue, int, TStatistic> createStatistic)
        where TValue : struct, Enum
    {
        var countsByValue = counts.ToDictionary(item => item.Value, item => item.Count);

        return Enum.GetValues<TValue>()
            .Select(value => createStatistic(
                value,
                countsByValue.GetValueOrDefault(value)))
            .ToList();
    }

    private static IReadOnlyList<FeedbackSeverityStatistic> CompleteSeverityBreakdown(
        IReadOnlyList<FeedbackSeverityCount> counts)
    {
        var countsBySeverity = counts.ToDictionary(item => item.Severity, item => item.Count);

        return Enumerable.Range(
                FeedbackAnalysis.MinimumSeverity,
                FeedbackAnalysis.MaximumSeverity - FeedbackAnalysis.MinimumSeverity + 1)
            .Select(severity => new FeedbackSeverityStatistic(
                severity,
                countsBySeverity.GetValueOrDefault(severity)))
            .ToList();
    }
}
