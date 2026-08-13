namespace PulsePilot.Application.Dashboard;

public interface IDashboardService
{
    Task<DashboardSummaryResponse> GetSummaryAsync(
        DashboardSummaryQuery query,
        CancellationToken cancellationToken = default);

    Task<DashboardTrendingResponse> GetTrendingAsync(
        DashboardTrendingQuery query,
        CancellationToken cancellationToken = default);
}
