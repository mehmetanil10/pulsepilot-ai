namespace PulsePilot.Application.Dashboard;

public sealed class DashboardSummaryQuery
{
    public int PeriodDays { get; init; } = 7;

    public int RecentFeedbackLimit { get; init; } = 5;

    public int PendingActionLimit { get; init; } = 4;
}

public sealed class DashboardTrendingQuery
{
    public int PeriodDays { get; init; } = 7;

    public int Limit { get; init; } = 5;
}
