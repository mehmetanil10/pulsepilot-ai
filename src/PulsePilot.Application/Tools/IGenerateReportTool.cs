using PulsePilot.Application.AI;

namespace PulsePilot.Application.Tools;

public interface IGenerateReportTool
{
    Task<GenerateReportToolResult> ExecuteAsync(
        Guid workspaceId,
        GenerateReportToolInput input,
        CancellationToken cancellationToken = default);
}

public sealed record GenerateReportToolInput(
    int? PeriodDays = null,
    int? TrendingIssueLimit = null);

public sealed record GenerateReportToolResult(
    DateTimeOffset GeneratedAt,
    GetFeedbackStatisticsToolResult Statistics,
    GetTrendingIssuesToolResult TrendingIssues,
    ProductReportResult Report);
