using PulsePilot.Application.Tools;

namespace PulsePilot.Application.Reports;

public interface IWeeklyReportService
{
    Task<GenerateReportToolResult> GenerateAsync(
        GenerateWeeklyReportCommand command,
        CancellationToken cancellationToken = default);
}
