using PulsePilot.Application.Abstractions.Authentication;
using PulsePilot.Application.Tools;

namespace PulsePilot.Application.Reports;

internal sealed class WeeklyReportService(
    IGenerateReportTool generateReportTool,
    ICurrentUserContext currentUser) : IWeeklyReportService
{
    public Task<GenerateReportToolResult> GenerateAsync(
        GenerateWeeklyReportCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return generateReportTool.ExecuteAsync(
            currentUser.WorkspaceId,
            new GenerateReportToolInput(
                command.PeriodDays,
                command.TrendingIssueLimit),
            cancellationToken);
    }
}
