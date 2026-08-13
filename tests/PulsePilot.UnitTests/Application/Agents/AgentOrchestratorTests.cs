using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PulsePilot.Application;
using PulsePilot.Application.Abstractions.AI;
using PulsePilot.Application.Agents;
using PulsePilot.Application.Common.Exceptions;

namespace PulsePilot.UnitTests.Application.Agents;

public sealed class AgentOrchestratorTests
{
    private static readonly AgentToolDefinition StatisticsTool = new(
        "get_feedback_statistics",
        "Returns aggregate feedback statistics for a bounded period.",
        """
        {
          "type": "object",
          "properties": {
            "periodDays": { "type": "integer" }
          },
          "additionalProperties": false
        }
        """);

    [Fact]
    public async Task RunAsync_ReturnsFinalAnswerWithoutExposingWorkspaceContext()
    {
        var workspaceId = Guid.CreateVersion7();
        var client = new SequenceAgentTurnClient(
            new AgentTurnResponse("  No critical issues were found.  ", []));
        var executor = new RecordingToolExecutor();
        await using var provider = CreateProvider(client, executor, []);
        await using var scope = provider.CreateAsyncScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IAgentOrchestrator>();

        var result = await orchestrator.RunAsync(
            workspaceId,
            "  What changed this week?  ");

        Assert.Equal("No critical issues were found.", result.Answer);
        Assert.Equal(1, result.ModelTurnCount);
        Assert.Equal(0, result.ToolCallCount);
        Assert.Empty(executor.Calls);
        var request = Assert.Single(client.Requests);
        Assert.Equal("What changed this week?", request.UserMessage);
        Assert.Empty(request.AvailableTools);
        Assert.Empty(request.PreviousToolExchanges);
        Assert.DoesNotContain(
            workspaceId.ToString(),
            JsonSerializer.Serialize(request),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_ExecutesAllowlistedCallsAndReturnsHistoryToNextTurn()
    {
        var workspaceId = Guid.CreateVersion7();
        var call = new AgentToolCall(
            "call_statistics_1",
            StatisticsTool.Name,
            "{\"periodDays\":7}");
        var client = new SequenceAgentTurnClient(
            new AgentTurnResponse(null, [call]),
            new AgentTurnResponse(
                "Feedback volume was stable during the last seven days.",
                []));
        var executor = new RecordingToolExecutor(
            new AgentToolExecutionOutput(
                true,
                "{\"totalFeedbackCount\":42,\"averageSeverity\":2.5}"));
        await using var provider = CreateProvider(client, executor, [StatisticsTool]);
        await using var scope = provider.CreateAsyncScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IAgentOrchestrator>();

        var result = await orchestrator.RunAsync(
            workspaceId,
            "Summarize feedback volume.");

        Assert.Equal(2, result.ModelTurnCount);
        var usage = Assert.Single(result.ToolUsages);
        Assert.Equal(call.CallId, usage.CallId);
        Assert.Equal(StatisticsTool.Name, usage.ToolName);
        Assert.True(usage.Succeeded);
        var execution = Assert.Single(executor.Calls);
        Assert.Equal(workspaceId, execution.WorkspaceId);
        Assert.Same(call, execution.Call);
        Assert.Equal(2, client.Requests.Count);
        Assert.Empty(client.Requests[0].PreviousToolExchanges);
        var exchange = Assert.Single(client.Requests[1].PreviousToolExchanges);
        Assert.Same(call, exchange.Call);
        Assert.True(exchange.Output.Succeeded);
        Assert.Contains("42", exchange.Output.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_RejectsInvalidOrUnallowlistedCallsBeforeExecution()
    {
        var invalidCalls = new[]
        {
            new AgentToolCall("call_1", "unknown_tool", "{}"),
            new AgentToolCall("call_1", StatisticsTool.Name, "[]"),
            new AgentToolCall("call_1", StatisticsTool.Name, "{not-json"),
        };

        foreach (var invalidCall in invalidCalls)
        {
            var client = new SequenceAgentTurnClient(
                new AgentTurnResponse(null, [invalidCall]));
            var executor = new RecordingToolExecutor();
            await using var provider = CreateProvider(client, executor, [StatisticsTool]);
            await using var scope = provider.CreateAsyncScope();
            var orchestrator = scope.ServiceProvider
                .GetRequiredService<IAgentOrchestrator>();

            var exception = await Assert.ThrowsAsync<LlmProviderException>(() =>
                orchestrator.RunAsync(Guid.CreateVersion7(), "Analyze feedback."));

            Assert.Equal(LlmProviderFailureKind.InvalidResponse, exception.FailureKind);
            Assert.Empty(executor.Calls);
        }
    }

    [Fact]
    public async Task RunAsync_RejectsDuplicateCallIdsAndMixedTurnModesBeforeExecution()
    {
        var duplicateCalls = new[]
        {
            new AgentToolCall("duplicate", StatisticsTool.Name, "{}"),
            new AgentToolCall("duplicate", StatisticsTool.Name, "{}"),
        };
        var responses = new[]
        {
            new AgentTurnResponse(null, duplicateCalls),
            new AgentTurnResponse(
                "An answer cannot accompany calls.",
                [new AgentToolCall("call_2", StatisticsTool.Name, "{}")]),
            new AgentTurnResponse(null, []),
            new AgentTurnResponse(new string('x', 8_001), []),
        };

        foreach (var response in responses)
        {
            var client = new SequenceAgentTurnClient(response);
            var executor = new RecordingToolExecutor();
            await using var provider = CreateProvider(client, executor, [StatisticsTool]);
            await using var scope = provider.CreateAsyncScope();
            var orchestrator = scope.ServiceProvider
                .GetRequiredService<IAgentOrchestrator>();

            var exception = await Assert.ThrowsAsync<LlmProviderException>(() =>
                orchestrator.RunAsync(Guid.CreateVersion7(), "Analyze feedback."));

            Assert.Equal(LlmProviderFailureKind.InvalidResponse, exception.FailureKind);
            Assert.Empty(executor.Calls);
        }
    }

    [Fact]
    public async Task RunAsync_EnforcesTotalBudgetBeforeAnyCallsInTurnExecute()
    {
        var calls = new[]
        {
            new AgentToolCall("call_1", StatisticsTool.Name, "{}"),
            new AgentToolCall("call_2", StatisticsTool.Name, "{}"),
        };
        var client = new SequenceAgentTurnClient(new AgentTurnResponse(null, calls));
        var executor = new RecordingToolExecutor();
        await using var provider = CreateProvider(
            client,
            executor,
            [StatisticsTool],
            options =>
            {
                options.MaxToolCallsPerTurn = 1;
                options.MaxTotalToolCalls = 1;
            });
        await using var scope = provider.CreateAsyncScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IAgentOrchestrator>();

        var exception = await Assert.ThrowsAsync<LlmProviderException>(() =>
            orchestrator.RunAsync(Guid.CreateVersion7(), "Analyze feedback."));

        Assert.Equal(LlmProviderFailureKind.Incomplete, exception.FailureKind);
        Assert.Empty(executor.Calls);
    }

    [Fact]
    public async Task RunAsync_DoesNotExecuteCallsWhenNoTurnRemainsForFinalAnswer()
    {
        var call = new AgentToolCall("call_1", StatisticsTool.Name, "{}");
        var client = new SequenceAgentTurnClient(
            new AgentTurnResponse(null, [call]));
        var executor = new RecordingToolExecutor();
        await using var provider = CreateProvider(
            client,
            executor,
            [StatisticsTool],
            options => options.MaxTurns = 1);
        await using var scope = provider.CreateAsyncScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IAgentOrchestrator>();

        var exception = await Assert.ThrowsAsync<LlmProviderException>(() =>
            orchestrator.RunAsync(Guid.CreateVersion7(), "Analyze feedback."));

        Assert.Equal(LlmProviderFailureKind.Incomplete, exception.FailureKind);
        Assert.Empty(executor.Calls);
    }

    [Fact]
    public async Task RunAsync_MapsInternalTimeoutAndPreservesCallerCancellation()
    {
        var client = new BlockingAgentTurnClient();
        await using var provider = CreateProvider(
            client,
            new RecordingToolExecutor(),
            [],
            options => options.ExecutionTimeoutSeconds = 1);
        await using var scope = provider.CreateAsyncScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IAgentOrchestrator>();

        var timeoutException = await Assert.ThrowsAsync<LlmProviderException>(() =>
            orchestrator.RunAsync(Guid.CreateVersion7(), "Analyze feedback."));

        Assert.Equal(
            LlmProviderFailureKind.ProviderUnavailable,
            timeoutException.FailureKind);
        Assert.True(timeoutException.IsTransient);

        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            orchestrator.RunAsync(
                Guid.CreateVersion7(),
                "Analyze feedback.",
                cancellationSource.Token));
    }

    [Fact]
    public async Task RunAsync_RejectsInvalidCatalogAndToolOutputAsBackendFailures()
    {
        var invalidCatalogs = new IReadOnlyList<AgentToolDefinition>[]
        {
            [StatisticsTool, StatisticsTool],
            [StatisticsTool with { Name = "Invalid-Tool" }],
            [StatisticsTool with { InputJsonSchema = "[]" }],
        };

        foreach (var catalog in invalidCatalogs)
        {
            var client = new SequenceAgentTurnClient(
                new AgentTurnResponse("Unused", []));
            await using var provider = CreateProvider(
                client,
                new RecordingToolExecutor(),
                catalog);
            await using var scope = provider.CreateAsyncScope();
            var orchestrator = scope.ServiceProvider
                .GetRequiredService<IAgentOrchestrator>();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                orchestrator.RunAsync(Guid.CreateVersion7(), "Analyze feedback."));
            Assert.Empty(client.Requests);
        }

        var call = new AgentToolCall("call_1", StatisticsTool.Name, "{}");
        var outputClient = new SequenceAgentTurnClient(
            new AgentTurnResponse(null, [call]));
        await using var outputProvider = CreateProvider(
            outputClient,
            new RecordingToolExecutor(new AgentToolExecutionOutput(true, " ")),
            [StatisticsTool]);
        await using var outputScope = outputProvider.CreateAsyncScope();
        var outputOrchestrator = outputScope.ServiceProvider
            .GetRequiredService<IAgentOrchestrator>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            outputOrchestrator.RunAsync(Guid.CreateVersion7(), "Analyze feedback."));
    }

    [Fact]
    public async Task DefaultRuntime_FailsClosedUntilToolCallingIsConfigured()
    {
        await using var provider = new ServiceCollection()
            .AddApplication()
            .BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IAgentOrchestrator>();

        var exception = await Assert.ThrowsAsync<LlmProviderException>(() =>
            orchestrator.RunAsync(Guid.CreateVersion7(), "Analyze feedback."));

        Assert.Equal(LlmProviderFailureKind.NotConfigured, exception.FailureKind);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RunAsync_RejectsInvalidTrustedContextOrUserInput(string message)
    {
        var client = new SequenceAgentTurnClient(
            new AgentTurnResponse("Unused", []));
        await using var provider = CreateProvider(
            client,
            new RecordingToolExecutor(),
            []);
        await using var scope = provider.CreateAsyncScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IAgentOrchestrator>();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            orchestrator.RunAsync(Guid.CreateVersion7(), message));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            orchestrator.RunAsync(Guid.Empty, "Valid message"));
        Assert.Empty(client.Requests);
    }

    private static ServiceProvider CreateProvider(
        IAgentTurnClient client,
        IAgentToolExecutor executor,
        IReadOnlyList<AgentToolDefinition> tools,
        Action<AgentOrchestrationOptions>? configureOptions = null)
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.RemoveAll<IAgentTurnClient>();
        services.RemoveAll<IAgentToolCatalog>();
        services.RemoveAll<IAgentToolExecutor>();
        services.AddSingleton(client);
        services.AddSingleton<IAgentToolCatalog>(new StaticToolCatalog(tools));
        services.AddSingleton(executor);

        if (configureOptions is not null)
        {
            services.Configure(configureOptions);
        }

        return services.BuildServiceProvider(validateScopes: true);
    }

    private sealed class SequenceAgentTurnClient(params AgentTurnResponse[] responses)
        : IAgentTurnClient
    {
        private readonly Queue<AgentTurnResponse> _responses = new(responses);

        public List<AgentTurnRequest> Requests { get; } = [];

        public Task<AgentTurnResponse> CreateTurnAsync(
            AgentTurnRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);

            return Task.FromResult(_responses.Dequeue());
        }
    }

    private sealed class BlockingAgentTurnClient : IAgentTurnClient
    {
        public async Task<AgentTurnResponse> CreateTurnAsync(
            AgentTurnRequest request,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

            throw new InvalidOperationException("The blocking client unexpectedly completed.");
        }
    }

    private sealed class StaticToolCatalog(IReadOnlyList<AgentToolDefinition> tools)
        : IAgentToolCatalog
    {
        public IReadOnlyList<AgentToolDefinition> ListTools() => tools;
    }

    private sealed class RecordingToolExecutor(
        AgentToolExecutionOutput? output = null) : IAgentToolExecutor
    {
        private readonly AgentToolExecutionOutput _output = output
            ?? new AgentToolExecutionOutput(true, "{}");

        public List<(Guid WorkspaceId, AgentToolCall Call)> Calls { get; } = [];

        public Task<AgentToolExecutionOutput> ExecuteAsync(
            Guid workspaceId,
            AgentToolCall toolCall,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add((workspaceId, toolCall));

            return Task.FromResult(_output);
        }
    }
}
