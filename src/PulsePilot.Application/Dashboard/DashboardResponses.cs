using PulsePilot.Domain.Actions;
using PulsePilot.Domain.Feedback;

namespace PulsePilot.Application.Dashboard;

public sealed record DashboardSummaryResponse(
    DateTimeOffset GeneratedAt,
    DateTimeOffset PeriodFromInclusive,
    int PeriodDays,
    DashboardKpisResponse Kpis,
    IReadOnlyList<DashboardCategoryCountResponse> Categories,
    IReadOnlyList<DashboardRecentFeedbackResponse> RecentFeedback,
    IReadOnlyList<DashboardPendingActionResponse> PendingActions);

public sealed record DashboardKpisResponse(
    int FeedbackToday,
    int AiProcessed,
    int CriticalIssues,
    int PendingActions,
    int ProcessingFailures,
    decimal? AverageSeverity);

public sealed record DashboardCategoryCountResponse(
    FeedbackCategory Category,
    int Count);

public sealed record DashboardRecentFeedbackResponse(
    Guid Id,
    string? Title,
    FeedbackSource Source,
    ProcessingStatus ProcessingStatus,
    DateTimeOffset CreatedAt);

public sealed record DashboardPendingActionResponse(
    Guid Id,
    PendingActionType ActionType,
    string Title,
    string Description,
    DateTimeOffset CreatedAt);

public sealed record DashboardTrendingResponse(
    DateTimeOffset PreviousFromInclusive,
    DateTimeOffset CurrentFromInclusive,
    DateTimeOffset CurrentToExclusive,
    int PeriodDays,
    IReadOnlyList<DashboardTrendingIssueResponse> Items);

public sealed record DashboardTrendingIssueResponse(
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
