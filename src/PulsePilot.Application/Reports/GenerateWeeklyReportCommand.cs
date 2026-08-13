namespace PulsePilot.Application.Reports;

public sealed record GenerateWeeklyReportCommand(
    int? PeriodDays = null,
    int? TrendingIssueLimit = null);
