using PulsePilot.Application.Abstractions.Authentication;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Application.Tools;
using PulsePilot.Domain.Actions;
using PulsePilot.Domain.Feedback;

namespace PulsePilot.Application.Dashboard;

internal sealed class DashboardService(
    IFeedbackStatisticsRepository feedbackStatisticsRepository,
    IFeedbackRepository feedbackRepository,
    IFeedbackClusterRepository feedbackClusterRepository,
    IPendingActionRepository pendingActionRepository,
    IGetFeedbackStatisticsTool feedbackStatisticsTool,
    IGetTrendingIssuesTool trendingIssuesTool,
    ICurrentUserContext currentUser) : IDashboardService
{
    public async Task<DashboardSummaryResponse> GetSummaryAsync(
        DashboardSummaryQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var workspaceId = currentUser.WorkspaceId;
        var periodStatistics = await feedbackStatisticsTool.ExecuteAsync(
            workspaceId,
            new GetFeedbackStatisticsToolInput(query.PeriodDays),
            cancellationToken);
        var generatedAt = periodStatistics.ToExclusive;
        var utcDayStart = new DateTimeOffset(
            generatedAt.Year,
            generatedAt.Month,
            generatedAt.Day,
            0,
            0,
            0,
            TimeSpan.Zero);
        var feedbackToday = generatedAt == utcDayStart
            ? 0
            : await feedbackStatisticsRepository.CountCreatedAsync(
                workspaceId,
                utcDayStart,
                generatedAt,
                cancellationToken);
        var criticalIssues = await feedbackClusterRepository.CountByPriorityAsync(
            workspaceId,
            FeedbackPriority.P1,
            cancellationToken);
        var pendingActionCount = await pendingActionRepository.CountAsync(
            workspaceId,
            PendingActionStatus.Pending,
            cancellationToken);
        var recentFeedback = await feedbackRepository.ListAsync(
            workspaceId,
            skip: 0,
            query.RecentFeedbackLimit,
            cancellationToken: cancellationToken);
        var pendingActions = await pendingActionRepository.ListAsync(
            workspaceId,
            PendingActionStatus.Pending,
            skip: 0,
            query.PendingActionLimit,
            cancellationToken);
        var processingFailures = periodStatistics.ProcessingStatuses
            .Single(item => item.Status == ProcessingStatus.Failed)
            .Count;

        return new DashboardSummaryResponse(
            generatedAt,
            periodStatistics.FromInclusive,
            query.PeriodDays,
            new DashboardKpisResponse(
                feedbackToday,
                periodStatistics.AnalyzedFeedbackCount,
                criticalIssues,
                pendingActionCount,
                processingFailures,
                periodStatistics.AverageSeverity),
            periodStatistics.Categories
                .Select(item => new DashboardCategoryCountResponse(
                    item.Category,
                    item.Count))
                .ToList(),
            recentFeedback
                .Select(item => new DashboardRecentFeedbackResponse(
                    item.Id,
                    item.Title,
                    item.Source,
                    item.ProcessingStatus,
                    item.CreatedAt))
                .ToList(),
            pendingActions
                .Select(item => new DashboardPendingActionResponse(
                    item.Id,
                    item.ActionType,
                    item.Title,
                    item.Description,
                    item.CreatedAt))
                .ToList());
    }

    public async Task<DashboardTrendingResponse> GetTrendingAsync(
        DashboardTrendingQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var result = await trendingIssuesTool.ExecuteAsync(
            currentUser.WorkspaceId,
            new GetTrendingIssuesToolInput(query.PeriodDays, query.Limit),
            cancellationToken);

        return new DashboardTrendingResponse(
            result.PreviousFromInclusive,
            result.CurrentFromInclusive,
            result.CurrentToExclusive,
            result.PeriodDays,
            result.Items.Select(item => new DashboardTrendingIssueResponse(
                item.FeedbackClusterId,
                item.Title,
                item.Category,
                item.Component,
                item.Priority,
                item.PriorityScore,
                item.CurrentPeriodCount,
                item.PreviousPeriodCount,
                item.DeltaCount,
                item.GrowthPercentage,
                item.IsNew)).ToList());
    }
}
