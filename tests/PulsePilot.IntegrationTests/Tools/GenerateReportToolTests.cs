using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PulsePilot.Application;
using PulsePilot.Application.Abstractions.AI;
using PulsePilot.Application.AI;
using PulsePilot.Application.Common.Exceptions;
using PulsePilot.Application.Tools;
using PulsePilot.IntegrationTests.Persistence;

namespace PulsePilot.IntegrationTests.Tools;

public sealed class GenerateReportToolTests(PostgreSqlFixture database)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly DateTimeOffset CurrentTime = new(
        2026,
        8,
        13,
        12,
        0,
        0,
        TimeSpan.Zero);

    private static readonly IReadOnlyDictionary<string, string?> Configuration =
        new Dictionary<string, string?>
        {
            ["FeedbackStatistics:DefaultPeriodDays"] = "7",
            ["FeedbackStatistics:MaxPeriodDays"] = "30",
            ["TrendingIssues:DefaultPeriodDays"] = "7",
            ["TrendingIssues:MaxPeriodDays"] = "30",
            ["TrendingIssues:DefaultLimit"] = "5",
            ["TrendingIssues:MaxLimit"] = "10",
            ["ReportGeneration:DefaultPeriodDays"] = "7",
            ["ReportGeneration:MaxPeriodDays"] = "30",
            ["ReportGeneration:DefaultTrendingIssueLimit"] = "5",
            ["ReportGeneration:MaxTrendingIssueLimit"] = "10",
            ["ReportGeneration:MaxAttempts"] = "2",
            ["ReportGeneration:TimeoutSeconds"] = "5",
            ["ReportGeneration:RetryDelayMilliseconds"] = "0",
        };

    [Fact]
    public async Task Tool_ComposesTrustedAggregateToolsAndReturnsStructuredReport()
    {
        var llmClient = new ReportLlmClient();
        await using var serviceProvider = CreateServiceProvider(llmClient);
        await using var scope = serviceProvider.CreateAsyncScope();
        var tool = scope.ServiceProvider.GetRequiredService<IGenerateReportTool>();
        var workspaceId = Guid.CreateVersion7();

        var result = await tool.ExecuteAsync(
            workspaceId,
            new GenerateReportToolInput());

        Assert.Equal(CurrentTime, result.GeneratedAt);
        Assert.Equal(CurrentTime.AddDays(-7), result.Statistics.FromInclusive);
        Assert.Equal(CurrentTime, result.Statistics.ToExclusive);
        Assert.Equal(0, result.Statistics.TotalFeedbackCount);
        Assert.Empty(result.TrendingIssues.Items);
        Assert.Equal("Weekly Product Intelligence Report", result.Report.Title);
        Assert.Equal(1, llmClient.CallCount);
        Assert.NotNull(llmClient.LastRequest);
        Assert.Equal(7, llmClient.LastRequest.PeriodDays);
        Assert.Equal(0, llmClient.LastRequest.TotalFeedbackCount);
        Assert.Equal(0, llmClient.LastRequest.AnalyzedFeedbackCount);
        Assert.Null(llmClient.LastRequest.AverageSeverity);
        Assert.All(llmClient.LastRequest.Categories, item => Assert.Equal(0, item.Count));
        Assert.All(llmClient.LastRequest.Components, item => Assert.Equal(0, item.Count));
        Assert.All(llmClient.LastRequest.Sentiments, item => Assert.Equal(0, item.Count));
        Assert.Empty(llmClient.LastRequest.TrendingIssues);
    }

    [Fact]
    public async Task Tool_RetriesTransientFailureAndRejectsUntrustedBounds()
    {
        var llmClient = new ReportLlmClient(transientFailuresBeforeSuccess: 1);
        await using var serviceProvider = CreateServiceProvider(llmClient);
        await using var scope = serviceProvider.CreateAsyncScope();
        var tool = scope.ServiceProvider.GetRequiredService<IGenerateReportTool>();
        var workspaceId = Guid.CreateVersion7();

        var result = await tool.ExecuteAsync(
            workspaceId,
            new GenerateReportToolInput(PeriodDays: 14, TrendingIssueLimit: 8));

        Assert.Equal(2, llmClient.CallCount);
        Assert.Equal(14, result.Statistics.PeriodDays);
        Assert.Equal(14, result.TrendingIssues.PeriodDays);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => tool.ExecuteAsync(
            workspaceId,
            new GenerateReportToolInput(PeriodDays: 0)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => tool.ExecuteAsync(
            workspaceId,
            new GenerateReportToolInput(PeriodDays: 31)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => tool.ExecuteAsync(
            workspaceId,
            new GenerateReportToolInput(TrendingIssueLimit: 0)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => tool.ExecuteAsync(
            workspaceId,
            new GenerateReportToolInput(TrendingIssueLimit: 11)));
        await Assert.ThrowsAsync<ArgumentException>(() => tool.ExecuteAsync(
            Guid.Empty,
            new GenerateReportToolInput()));
        await Assert.ThrowsAsync<ArgumentNullException>(() => tool.ExecuteAsync(
            workspaceId,
            null!));
    }

    [Fact]
    public async Task Tool_DoesNotRetryPermanentProviderFailure()
    {
        var llmClient = new ReportLlmClient(
            terminalFailure: new LlmProviderException(
                LlmProviderFailureKind.Refused,
                "The provider permanently refused the report request."));
        await using var serviceProvider = CreateServiceProvider(llmClient);
        await using var scope = serviceProvider.CreateAsyncScope();
        var tool = scope.ServiceProvider.GetRequiredService<IGenerateReportTool>();

        var exception = await Assert.ThrowsAsync<LlmProviderException>(() =>
            tool.ExecuteAsync(Guid.CreateVersion7(), new GenerateReportToolInput()));

        Assert.Equal(LlmProviderFailureKind.Refused, exception.FailureKind);
        Assert.Equal(1, llmClient.CallCount);
    }

    [Fact]
    public async Task Tool_StopsAfterConfiguredTransientAttemptLimit()
    {
        var llmClient = new ReportLlmClient(transientFailuresBeforeSuccess: int.MaxValue);
        await using var serviceProvider = CreateServiceProvider(llmClient);
        await using var scope = serviceProvider.CreateAsyncScope();
        var tool = scope.ServiceProvider.GetRequiredService<IGenerateReportTool>();

        var exception = await Assert.ThrowsAsync<LlmProviderException>(() =>
            tool.ExecuteAsync(Guid.CreateVersion7(), new GenerateReportToolInput()));

        Assert.True(exception.IsTransient);
        Assert.Equal(2, llmClient.CallCount);
    }

    private ServiceProvider CreateServiceProvider(ReportLlmClient llmClient)
    {
        return database.CreateServiceProvider(
            Configuration,
            services =>
            {
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(CurrentTime));
                services.AddApplication();
                services.RemoveAll<ILLMClient>();
                services.AddSingleton<ILLMClient>(llmClient);
            });
    }

    private sealed class ReportLlmClient(
        int transientFailuresBeforeSuccess = 0,
        LlmProviderException? terminalFailure = null) : ILLMClient
    {
        private int _callCount;

        public int CallCount => _callCount;

        public ProductReportRequest? LastRequest { get; private set; }

        public Task<FeedbackAnalysisResult> AnalyzeFeedbackAsync(
            FeedbackAnalysisRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<FeedbackEmbeddingResult> GenerateEmbeddingAsync(
            FeedbackEmbeddingRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<CustomerResponseDraftResult> GenerateResponseDraftAsync(
            CustomerResponseDraftRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ProductReportResult> GenerateReportAsync(
            ProductReportRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequest = request;
            var callCount = Interlocked.Increment(ref _callCount);

            if (callCount <= transientFailuresBeforeSuccess)
            {
                throw new LlmProviderException(
                    LlmProviderFailureKind.ProviderUnavailable,
                    "Temporary report provider failure.",
                    isTransient: true);
            }

            if (terminalFailure is not null)
            {
                throw terminalFailure;
            }

            return Task.FromResult(new ProductReportResult(
                "Weekly Product Intelligence Report",
                request.TotalFeedbackCount == 0
                    ? "No feedback was received during the selected period."
                    : $"{request.TotalFeedbackCount} feedback records were received.",
                ["The report reflects only validated aggregate metrics."],
                ["Continue monitoring product feedback trends."]));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
