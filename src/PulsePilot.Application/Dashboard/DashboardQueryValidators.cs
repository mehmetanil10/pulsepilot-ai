using FluentValidation;
using Microsoft.Extensions.Options;
using PulsePilot.Application.Tools;

namespace PulsePilot.Application.Dashboard;

public sealed class DashboardSummaryQueryValidator : AbstractValidator<DashboardSummaryQuery>
{
    public const int MaximumPreviewLimit = 10;

    public DashboardSummaryQueryValidator(IOptions<FeedbackStatisticsOptions> options)
    {
        var maximumPeriodDays = options.Value.MaxPeriodDays;

        RuleFor(query => query.PeriodDays)
            .InclusiveBetween(1, maximumPeriodDays);
        RuleFor(query => query.RecentFeedbackLimit)
            .InclusiveBetween(1, MaximumPreviewLimit);
        RuleFor(query => query.PendingActionLimit)
            .InclusiveBetween(1, MaximumPreviewLimit);
    }
}

public sealed class DashboardTrendingQueryValidator : AbstractValidator<DashboardTrendingQuery>
{
    public DashboardTrendingQueryValidator(IOptions<TrendingIssuesOptions> options)
    {
        var configured = options.Value;

        RuleFor(query => query.PeriodDays)
            .InclusiveBetween(1, configured.MaxPeriodDays);
        RuleFor(query => query.Limit)
            .InclusiveBetween(1, configured.MaxLimit);
    }
}
