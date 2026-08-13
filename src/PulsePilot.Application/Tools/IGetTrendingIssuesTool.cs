using PulsePilot.Domain.Feedback;

namespace PulsePilot.Application.Tools;

public interface IGetTrendingIssuesTool
{
    Task<GetTrendingIssuesToolResult> ExecuteAsync(
        Guid workspaceId,
        GetTrendingIssuesToolInput input,
        CancellationToken cancellationToken = default);
}

public sealed record GetTrendingIssuesToolInput(
    int? PeriodDays = null,
    int? Limit = null);

public sealed record GetTrendingIssuesToolResult(
    DateTimeOffset PreviousFromInclusive,
    DateTimeOffset CurrentFromInclusive,
    DateTimeOffset CurrentToExclusive,
    int PeriodDays,
    IReadOnlyList<TrendingIssueToolItem> Items)
{
    public int Count => Items.Count;
}

public sealed record TrendingIssueToolItem(
    Guid FeedbackClusterId,
    string Title,
    FeedbackCategory Category,
    FeedbackComponent Component,
    FeedbackPriority Priority,
    decimal PriorityScore,
    int CurrentPeriodCount,
    int PreviousPeriodCount,
    int DeltaCount,
    decimal? GrowthPercentage,
    bool IsNew);
