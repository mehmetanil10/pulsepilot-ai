using FluentValidation;
using Microsoft.Extensions.Options;
using PulsePilot.Application.Abstractions.AI;
using PulsePilot.Application.AI;
using PulsePilot.Application.Common.Exceptions;

namespace PulsePilot.Application.Tools;

internal sealed class GenerateReportTool(
    IGetFeedbackStatisticsTool feedbackStatisticsTool,
    IGetTrendingIssuesTool trendingIssuesTool,
    ILLMClient llmClient,
    IValidator<ProductReportResult> reportResultValidator,
    IOptions<ReportGenerationOptions> options) : IGenerateReportTool
{
    private readonly ReportGenerationOptions _options = options.Value;

    public async Task<GenerateReportToolResult> ExecuteAsync(
        Guid workspaceId,
        GenerateReportToolInput input,
        CancellationToken cancellationToken = default)
    {
        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("Workspace id is required.", nameof(workspaceId));
        }

        ArgumentNullException.ThrowIfNull(input);
        var periodDays = input.PeriodDays ?? _options.DefaultPeriodDays;
        var trendingIssueLimit = input.TrendingIssueLimit
            ?? _options.DefaultTrendingIssueLimit;

        if (periodDays is < 1 || periodDays > _options.MaxPeriodDays)
        {
            throw new ArgumentOutOfRangeException(
                nameof(input),
                $"Report period must be between 1 and {_options.MaxPeriodDays} days.");
        }

        if (trendingIssueLimit is < 1
            || trendingIssueLimit > _options.MaxTrendingIssueLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(input),
                $"Trending issue limit must be between 1 and {_options.MaxTrendingIssueLimit}.");
        }

        var statistics = await feedbackStatisticsTool.ExecuteAsync(
            workspaceId,
            new GetFeedbackStatisticsToolInput(periodDays),
            cancellationToken);
        var trendingIssues = await trendingIssuesTool.ExecuteAsync(
            workspaceId,
            new GetTrendingIssuesToolInput(periodDays, trendingIssueLimit),
            cancellationToken);
        var request = CreateRequest(statistics, trendingIssues);
        var report = await GenerateWithRetryAsync(request, cancellationToken);
        var validationResult = await reportResultValidator.ValidateAsync(
            report,
            cancellationToken);

        if (!validationResult.IsValid)
        {
            throw new LlmProviderException(
                LlmProviderFailureKind.InvalidResponse,
                "The AI provider returned a product report outside the accepted contract.");
        }

        report = report.Normalize();

        return new GenerateReportToolResult(
            statistics.ToExclusive,
            statistics,
            trendingIssues,
            report);
    }

    private async Task<ProductReportResult> GenerateWithRetryAsync(
        ProductReportRequest request,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= _options.MaxAttempts; attempt++)
        {
            try
            {
                using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);
                timeoutSource.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

                return await llmClient.GenerateReportAsync(request, timeoutSource.Token);
            }
            catch (LlmProviderException exception)
                when (exception.IsTransient && attempt < _options.MaxAttempts)
            {
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                if (attempt == _options.MaxAttempts)
                {
                    throw new LlmProviderException(
                        LlmProviderFailureKind.ProviderUnavailable,
                        "Product report generation timed out.",
                        isTransient: true);
                }
            }

            await Task.Delay(
                TimeSpan.FromMilliseconds(_options.RetryDelayMilliseconds * attempt),
                cancellationToken);
        }

        throw new InvalidOperationException(
            "Product report generation retry loop completed unexpectedly.");
    }

    private static ProductReportRequest CreateRequest(
        GetFeedbackStatisticsToolResult statistics,
        GetTrendingIssuesToolResult trendingIssues)
    {
        return new ProductReportRequest(
            statistics.FromInclusive,
            statistics.ToExclusive,
            statistics.PeriodDays,
            statistics.TotalFeedbackCount,
            statistics.AnalyzedFeedbackCount,
            statistics.AverageSeverity,
            statistics.Categories
                .Select(item => new ProductReportBreakdownItem(
                    item.Category.ToString(),
                    item.Count))
                .ToList(),
            statistics.Components
                .Select(item => new ProductReportBreakdownItem(
                    item.Component.ToString(),
                    item.Count))
                .ToList(),
            statistics.Sentiments
                .Select(item => new ProductReportBreakdownItem(
                    item.Sentiment.ToString(),
                    item.Count))
                .ToList(),
            trendingIssues.Items
                .Select(item => new ProductReportTrendingIssue(
                    item.Category.ToString(),
                    item.Component.ToString(),
                    item.Priority.ToString(),
                    item.PriorityScore,
                    item.CurrentPeriodCount,
                    item.PreviousPeriodCount,
                    item.DeltaCount,
                    item.GrowthPercentage,
                    item.IsNew))
                .ToList());
    }
}
