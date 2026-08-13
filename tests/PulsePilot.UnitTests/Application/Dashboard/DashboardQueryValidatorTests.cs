using Microsoft.Extensions.Options;
using PulsePilot.Application.Dashboard;
using PulsePilot.Application.Tools;

namespace PulsePilot.UnitTests.Application.Dashboard;

public sealed class DashboardQueryValidatorTests
{
    [Fact]
    public async Task SummaryQuery_ValidatesPeriodAndPreviewBounds()
    {
        var validator = new DashboardSummaryQueryValidator(
            Options.Create(new FeedbackStatisticsOptions()));

        var valid = await validator.ValidateAsync(new DashboardSummaryQuery());
        var invalid = await validator.ValidateAsync(new DashboardSummaryQuery
        {
            PeriodDays = 0,
            RecentFeedbackLimit = 11,
            PendingActionLimit = 0,
        });

        Assert.True(valid.IsValid);
        Assert.False(invalid.IsValid);
        Assert.Equal(3, invalid.Errors.Count);
    }

    [Fact]
    public async Task TrendingQuery_ValidatesPeriodAndLimitBounds()
    {
        var validator = new DashboardTrendingQueryValidator(
            Options.Create(new TrendingIssuesOptions()));

        var valid = await validator.ValidateAsync(new DashboardTrendingQuery());
        var invalid = await validator.ValidateAsync(new DashboardTrendingQuery
        {
            PeriodDays = 366,
            Limit = 51,
        });

        Assert.True(valid.IsValid);
        Assert.False(invalid.IsValid);
        Assert.Equal(2, invalid.Errors.Count);
    }
}
