using Microsoft.Extensions.Options;
using PulsePilot.Application.Abstractions.Persistence;

namespace PulsePilot.Application.Tools;

internal sealed class GetTrendingIssuesTool(
    ITrendingIssueRepository trendingIssueRepository,
    IOptions<TrendingIssuesOptions> trendingIssuesOptions,
    TimeProvider timeProvider) : IGetTrendingIssuesTool
{
    private readonly TrendingIssuesOptions _options = trendingIssuesOptions.Value;

    public async Task<GetTrendingIssuesToolResult> ExecuteAsync(
        Guid workspaceId,
        GetTrendingIssuesToolInput input,
        CancellationToken cancellationToken = default)
    {
        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("Workspace id is required.", nameof(workspaceId));
        }

        ArgumentNullException.ThrowIfNull(input);
        var periodDays = input.PeriodDays ?? _options.DefaultPeriodDays;
        var limit = input.Limit ?? _options.DefaultLimit;

        if (periodDays is < 1 || periodDays > _options.MaxPeriodDays)
        {
            throw new ArgumentOutOfRangeException(
                nameof(input),
                $"Trending issue period must be between 1 and {_options.MaxPeriodDays} days.");
        }

        if (limit is < 1 || limit > _options.MaxLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(input),
                $"Trending issue limit must be between 1 and {_options.MaxLimit}.");
        }

        var currentToExclusive = timeProvider.GetUtcNow().ToUniversalTime();
        var currentFromInclusive = currentToExclusive.AddDays(-periodDays);
        var previousFromInclusive = currentFromInclusive.AddDays(-periodDays);
        var snapshots = await trendingIssueRepository.ListAsync(
            workspaceId,
            previousFromInclusive,
            currentFromInclusive,
            currentToExclusive,
            limit,
            cancellationToken);

        return new GetTrendingIssuesToolResult(
            previousFromInclusive,
            currentFromInclusive,
            currentToExclusive,
            periodDays,
            snapshots.Select(CreateItem).ToList());
    }

    private static TrendingIssueToolItem CreateItem(TrendingIssueSnapshot snapshot)
    {
        var deltaCount = snapshot.CurrentPeriodCount - snapshot.PreviousPeriodCount;
        var isNew = snapshot.PreviousPeriodCount == 0;
        decimal? growthPercentage = isNew
            ? null
            : decimal.Round(
                deltaCount * 100m / snapshot.PreviousPeriodCount,
                2,
                MidpointRounding.AwayFromZero);

        return new TrendingIssueToolItem(
            snapshot.FeedbackClusterId,
            snapshot.Title,
            snapshot.Category,
            snapshot.Component,
            snapshot.Priority,
            snapshot.PriorityScore,
            snapshot.CurrentPeriodCount,
            snapshot.PreviousPeriodCount,
            deltaCount,
            growthPercentage,
            isNew);
    }
}
