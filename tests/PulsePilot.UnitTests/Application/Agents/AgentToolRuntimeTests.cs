using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PulsePilot.Application;
using PulsePilot.Application.Agents;
using PulsePilot.Application.AI;
using PulsePilot.Application.Common.Exceptions;
using PulsePilot.Application.Tools;
using PulsePilot.Domain.Feedback;

namespace PulsePilot.UnitTests.Application.Agents;

public sealed class AgentToolRuntimeTests
{
    private static readonly Guid WorkspaceId =
        Guid.Parse("0198a891-57b0-7000-8000-000000000101");
    private static readonly Guid FeedbackId =
        Guid.Parse("0198a891-57b0-7000-8000-000000000102");

    [Fact]
    public void Catalog_ExposesOnlyStrictReadOnlyAndAnalyticalTools()
    {
        using var provider = new ServiceCollection()
            .AddApplication()
            .BuildServiceProvider(validateScopes: true);
        var catalog = provider.GetRequiredService<IAgentToolCatalog>();

        var tools = catalog.ListTools();

        Assert.Equal(
            [
                AgentToolNames.SearchSimilarFeedback,
                AgentToolNames.GetFeedbackStatistics,
                AgentToolNames.GetTrendingIssues,
                AgentToolNames.GenerateReport,
            ],
            tools.Select(tool => tool.Name));
        Assert.DoesNotContain(tools, tool =>
            tool.Name.Contains("create", StringComparison.Ordinal)
            || tool.Name.Contains("draft", StringComparison.Ordinal)
            || tool.Name.Contains("send", StringComparison.Ordinal));

        foreach (var tool in tools)
        {
            using var schemaDocument = JsonDocument.Parse(tool.InputJsonSchema);
            var schema = schemaDocument.RootElement;
            var properties = schema.GetProperty("properties");
            var required = schema.GetProperty("required")
                .EnumerateArray()
                .Select(item => item.GetString())
                .ToHashSet(StringComparer.Ordinal);

            Assert.Equal("object", schema.GetProperty("type").GetString());
            Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
            Assert.Equal(properties.EnumerateObject().Count(), required.Count);
            Assert.All(properties.EnumerateObject(), property =>
                Assert.Contains(property.Name, required));
            Assert.DoesNotContain("workspaceId", tool.InputJsonSchema, StringComparison.Ordinal);
            Assert.Contains("Returns", tool.Description, StringComparison.Ordinal);
            Assert.Contains("failure", tool.Description, StringComparison.OrdinalIgnoreCase);
        }

        var searchSchema = JsonDocument.Parse(tools[0].InputJsonSchema);
        var limitTypes = searchSchema.RootElement
            .GetProperty("properties")
            .GetProperty("limit")
            .GetProperty("type")
            .EnumerateArray()
            .Select(item => item.GetString());
        Assert.Equal(["integer", "null"], limitTypes);
        Assert.Equal(
            10,
            searchSchema.RootElement
                .GetProperty("properties")
                .GetProperty("limit")
                .GetProperty("maximum")
                .GetInt32());
    }

    [Fact]
    public async Task Executor_DispatchesEveryAllowlistedToolWithTrustedWorkspaceContext()
    {
        var fakes = new RecordingTools();
        await using var provider = CreateProvider(fakes);
        await using var scope = provider.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<IAgentToolExecutor>();

        var outputs = new[]
        {
            await executor.ExecuteAsync(
                WorkspaceId,
                new AgentToolCall(
                    "call_search",
                    AgentToolNames.SearchSimilarFeedback,
                    $"{{\"feedbackId\":\"{FeedbackId}\",\"limit\":3}}")),
            await executor.ExecuteAsync(
                WorkspaceId,
                new AgentToolCall(
                    "call_statistics",
                    AgentToolNames.GetFeedbackStatistics,
                    "{\"periodDays\":7}")),
            await executor.ExecuteAsync(
                WorkspaceId,
                new AgentToolCall(
                    "call_trends",
                    AgentToolNames.GetTrendingIssues,
                    "{\"periodDays\":7,\"limit\":5}")),
            await executor.ExecuteAsync(
                WorkspaceId,
                new AgentToolCall(
                    "call_report",
                    AgentToolNames.GenerateReport,
                    "{\"periodDays\":7,\"trendingIssueLimit\":5}")),
        };

        Assert.All(outputs, output => Assert.True(output.Succeeded));
        Assert.All(outputs, output => JsonDocument.Parse(output.Content).Dispose());
        Assert.Equal(4, fakes.WorkspaceIds.Count);
        Assert.All(fakes.WorkspaceIds, id => Assert.Equal(WorkspaceId, id));
        Assert.Equal(new SearchSimilarFeedbackToolInput(FeedbackId, 3), fakes.SearchInput);
        Assert.Equal(new GetFeedbackStatisticsToolInput(7), fakes.StatisticsInput);
        Assert.Equal(new GetTrendingIssuesToolInput(7, 5), fakes.TrendingInput);
        Assert.Equal(new GenerateReportToolInput(7, 5), fakes.ReportInput);

        using var searchOutput = JsonDocument.Parse(outputs[0].Content);
        var content = searchOutput.RootElement
            .GetProperty("items")[0]
            .GetProperty("content")
            .GetString();
        Assert.NotNull(content);
        Assert.Equal(1_000, content.Length);
        Assert.Equal("Manual", searchOutput.RootElement
            .GetProperty("items")[0]
            .GetProperty("source")
            .GetString());
    }

    public static TheoryData<string> InvalidStatisticsArguments => new()
    {
        "{}",
        "[]",
        "{\"PeriodDays\":7}",
        "{\"periodDays\":0}",
        "{\"periodDays\":366}",
        "{\"periodDays\":7,\"workspaceId\":\"0198a891-57b0-7000-8000-000000000101\"}",
        "{\"periodDays\":7,\"periodDays\":8}",
    };

    [Theory]
    [MemberData(nameof(InvalidStatisticsArguments))]
    public async Task Executor_ReturnsSafeFailureForInvalidArguments(string argumentsJson)
    {
        var fakes = new RecordingTools();
        await using var provider = CreateProvider(fakes);
        await using var scope = provider.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<IAgentToolExecutor>();

        var output = await executor.ExecuteAsync(
            WorkspaceId,
            new AgentToolCall(
                "call_invalid",
                AgentToolNames.GetFeedbackStatistics,
                argumentsJson));

        Assert.False(output.Succeeded);
        Assert.Empty(fakes.WorkspaceIds);
        using var outputDocument = JsonDocument.Parse(output.Content);
        Assert.Equal(
            "invalid_arguments",
            outputDocument.RootElement
                .GetProperty("error")
                .GetProperty("code")
                .GetString());
        Assert.DoesNotContain(argumentsJson, output.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Executor_MapsExpectedToolFailureWithoutLeakingDetails()
    {
        var fakes = new RecordingTools
        {
            StatisticsException = new NotFoundException(
                "Sensitive tenant resource 0198a891 was not found."),
        };
        await using var provider = CreateProvider(fakes);
        await using var scope = provider.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<IAgentToolExecutor>();

        var output = await executor.ExecuteAsync(
            WorkspaceId,
            new AgentToolCall(
                "call_missing",
                AgentToolNames.GetFeedbackStatistics,
                "{\"periodDays\":null}"));

        Assert.False(output.Succeeded);
        Assert.Contains("not_found", output.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("0198a891", output.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Sensitive", output.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Executor_RejectsUnknownBackendTool()
    {
        await using var provider = CreateProvider(new RecordingTools());
        await using var scope = provider.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<IAgentToolExecutor>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync(
                WorkspaceId,
                new AgentToolCall("call_unknown", "create_backlog_item", "{}")));
    }

    private static ServiceProvider CreateProvider(RecordingTools fakes)
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.RemoveAll<ISearchSimilarFeedbackTool>();
        services.RemoveAll<IGetFeedbackStatisticsTool>();
        services.RemoveAll<IGetTrendingIssuesTool>();
        services.RemoveAll<IGenerateReportTool>();
        services.RemoveAll<IAgentToolExecutor>();
        services.AddSingleton<ISearchSimilarFeedbackTool>(fakes);
        services.AddSingleton<IGetFeedbackStatisticsTool>(fakes);
        services.AddSingleton<IGetTrendingIssuesTool>(fakes);
        services.AddSingleton<IGenerateReportTool>(fakes);
        services.AddScoped<IAgentToolExecutor, AgentToolExecutor>();

        return services.BuildServiceProvider(validateScopes: true);
    }

    private sealed class RecordingTools :
        ISearchSimilarFeedbackTool,
        IGetFeedbackStatisticsTool,
        IGetTrendingIssuesTool,
        IGenerateReportTool
    {
        private static readonly DateTimeOffset Now =
            new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);

        public List<Guid> WorkspaceIds { get; } = [];

        public SearchSimilarFeedbackToolInput? SearchInput { get; private set; }

        public GetFeedbackStatisticsToolInput? StatisticsInput { get; private set; }

        public GetTrendingIssuesToolInput? TrendingInput { get; private set; }

        public GenerateReportToolInput? ReportInput { get; private set; }

        public Exception? StatisticsException { get; init; }

        public Task<SearchSimilarFeedbackToolResult> ExecuteAsync(
            Guid workspaceId,
            SearchSimilarFeedbackToolInput input,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WorkspaceIds.Add(workspaceId);
            SearchInput = input;

            return Task.FromResult(new SearchSimilarFeedbackToolResult(
                input.FeedbackId,
                0.8,
                [
                    new SearchSimilarFeedbackToolItem(
                        Guid.CreateVersion7(),
                        Guid.CreateVersion7(),
                        "Payment issue",
                        new string('x', 1_100),
                        FeedbackSource.Manual,
                        0.91,
                        Now),
                ]));
        }

        public Task<GetFeedbackStatisticsToolResult> ExecuteAsync(
            Guid workspaceId,
            GetFeedbackStatisticsToolInput input,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WorkspaceIds.Add(workspaceId);
            StatisticsInput = input;

            if (StatisticsException is not null)
            {
                throw StatisticsException;
            }

            return Task.FromResult(CreateStatistics(input.PeriodDays ?? 7));
        }

        public Task<GetTrendingIssuesToolResult> ExecuteAsync(
            Guid workspaceId,
            GetTrendingIssuesToolInput input,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WorkspaceIds.Add(workspaceId);
            TrendingInput = input;

            return Task.FromResult(CreateTrends(input.PeriodDays ?? 7));
        }

        public Task<GenerateReportToolResult> ExecuteAsync(
            Guid workspaceId,
            GenerateReportToolInput input,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WorkspaceIds.Add(workspaceId);
            ReportInput = input;
            var periodDays = input.PeriodDays ?? 7;

            return Task.FromResult(new GenerateReportToolResult(
                Now,
                CreateStatistics(periodDays),
                CreateTrends(periodDays),
                new ProductReportResult(
                    "Weekly report",
                    "Feedback was stable.",
                    ["No material volume change."],
                    ["Continue monitoring."])));
        }

        private static GetFeedbackStatisticsToolResult CreateStatistics(int periodDays)
        {
            return new GetFeedbackStatisticsToolResult(
                Now.AddDays(-periodDays),
                Now,
                periodDays,
                12,
                10,
                2.5m,
                [],
                [],
                [],
                [],
                [],
                []);
        }

        private static GetTrendingIssuesToolResult CreateTrends(int periodDays)
        {
            return new GetTrendingIssuesToolResult(
                Now.AddDays(-(periodDays * 2)),
                Now.AddDays(-periodDays),
                Now,
                periodDays,
                []);
        }
    }
}
