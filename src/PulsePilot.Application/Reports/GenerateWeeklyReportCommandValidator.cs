using FluentValidation;
using Microsoft.Extensions.Options;
using PulsePilot.Application.Tools;

namespace PulsePilot.Application.Reports;

public sealed class GenerateWeeklyReportCommandValidator
    : AbstractValidator<GenerateWeeklyReportCommand>
{
    public GenerateWeeklyReportCommandValidator(IOptions<ReportGenerationOptions> options)
    {
        var reportOptions = options.Value;

        RuleFor(command => command.PeriodDays)
            .InclusiveBetween(1, reportOptions.MaxPeriodDays)
            .When(command => command.PeriodDays.HasValue);
        RuleFor(command => command.TrendingIssueLimit)
            .InclusiveBetween(1, reportOptions.MaxTrendingIssueLimit)
            .When(command => command.TrendingIssueLimit.HasValue);
    }
}
