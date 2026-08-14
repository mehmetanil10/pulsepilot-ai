using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PulsePilot.Application.Abstractions.AI;
using PulsePilot.Application.Actions;
using PulsePilot.Application.Agents;
using PulsePilot.Application.AI;
using PulsePilot.Application.Authentication;
using PulsePilot.Application.Backlog;
using PulsePilot.Application.Copilot;
using PulsePilot.Application.Feedback;
using PulsePilot.Application.FeedbackProcessing;
using PulsePilot.Domain.Actions;
using PulsePilot.Domain.Backlog;
using PulsePilot.Domain.Feedback;
using PulsePilot.IntegrationTests.Api;
using PulsePilot.IntegrationTests.Persistence;

namespace PulsePilot.IntegrationTests.Acceptance;

public sealed class Sprint3AcceptanceTests(PostgreSqlFixture database)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly JsonSerializerOptions SerializerOptions =
        CreateSerializerOptions();

    [Fact]
    public async Task HumanControlledIntelligenceFlow_CompletesSprint3DefinitionOfDone()
    {
        var llmClient = new DeterministicLlmClient();
        var agentClient = new StatisticsAgentTurnClient();
        await using var factory = new PulsePilotApiFactory(
            database.ConnectionString,
            services =>
            {
                services.RemoveAll<ILLMClient>();
                services.RemoveAll<IAgentTurnClient>();
                services.AddSingleton<ILLMClient>(llmClient);
                services.AddSingleton<IAgentTurnClient>(agentClient);
            });
        using var client = await CreateAuthenticatedClientAsync(factory);

        var first = await CreateFeedbackAsync(
            client,
            "Checkout rejects valid cards",
            "Customers cannot submit a valid payment card.");
        var second = await CreateFeedbackAsync(
            client,
            "Payment form freezes",
            "The checkout payment form freezes before submission.");

        await ProcessNextAsync(factory.Services);
        await ProcessNextAsync(factory.Services);

        var actions = await client.GetFromJsonAsync<PendingActionListResponse>(
            "/api/actions?status=pending",
            SerializerOptions);
        Assert.NotNull(actions);
        var pendingAction = Assert.Single(actions.Items);
        Assert.Equal(PendingActionType.CreateEngineeringIssue, pendingAction.ActionType);
        Assert.Equal(PendingActionStatus.Pending, pendingAction.Status);

        using var approvalResponse = await client.PostAsync(
            $"/api/actions/{pendingAction.Id}/approve",
            content: null);
        approvalResponse.EnsureSuccessStatusCode();
        var executedAction = await approvalResponse.Content
            .ReadFromJsonAsync<PendingActionResponse>(SerializerOptions);

        Assert.NotNull(executedAction);
        Assert.Equal(PendingActionStatus.Executed, executedAction.Status);

        var backlog = await client.GetFromJsonAsync<BacklogItemListResponse>(
            $"/api/backlog?sourcePendingActionId={pendingAction.Id}",
            SerializerOptions);
        Assert.NotNull(backlog);
        var backlogItem = Assert.Single(backlog.Items);
        Assert.Equal(pendingAction.Id, backlogItem.SourcePendingActionId);
        Assert.Equal(BacklogItemPriority.P2, backlogItem.Priority);
        Assert.Equal(BacklogItemStatus.Open, backlogItem.Status);

        using var copilotResponse = await client.PostAsJsonAsync(
            "/api/copilot/chat",
            new CopilotChatCommand("How much feedback is in this workspace?"),
            SerializerOptions);
        copilotResponse.EnsureSuccessStatusCode();
        var copilot = await copilotResponse.Content
            .ReadFromJsonAsync<CopilotChatResponse>(SerializerOptions);

        Assert.NotNull(copilot);
        Assert.Equal("Workspace contains 2 feedback items.", copilot.Answer);
        Assert.Equal(2, copilot.ModelTurnCount);
        Assert.Equal(1, copilot.ToolCallCount);
        Assert.Equal(AgentToolNames.GetFeedbackStatistics, Assert.Single(copilot.ToolUsages).ToolName);
        Assert.Equal(2, llmClient.AnalysisCallCount);
        Assert.Equal(2, llmClient.EmbeddingCallCount);
        Assert.Equal(2, agentClient.Requests.Count);

        var firstCompleted = await client.GetFromJsonAsync<FeedbackResponse>(
            $"/api/feedback/{first.Id}",
            SerializerOptions);
        var secondCompleted = await client.GetFromJsonAsync<FeedbackResponse>(
            $"/api/feedback/{second.Id}",
            SerializerOptions);
        Assert.NotNull(firstCompleted);
        Assert.NotNull(secondCompleted);
        Assert.Equal(ProcessingStatus.Completed, firstCompleted.ProcessingStatus);
        Assert.Equal(firstCompleted.FeedbackClusterId, secondCompleted.FeedbackClusterId);
    }

    private static async Task<HttpClient> CreateAuthenticatedClientAsync(
        PulsePilotApiFactory factory)
    {
        var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterCommand(
                $"sprint3-{Guid.CreateVersion7():N}@example.com",
                "Sprint 3 Owner",
                "correct-horse-battery-staple",
                "Sprint 3 Acceptance"),
            SerializerOptions);
        response.EnsureSuccessStatusCode();
        var authentication = await response.Content
            .ReadFromJsonAsync<AuthenticationResponse>(SerializerOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            authentication!.AccessToken);

        return client;
    }

    private static async Task<FeedbackResponse> CreateFeedbackAsync(
        HttpClient client,
        string title,
        string content)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/feedback",
            new CreateFeedbackCommand(
                title,
                content,
                FeedbackSource.Api,
                null,
                $"{Guid.CreateVersion7():N}@example.com"),
            SerializerOptions);
        response.EnsureSuccessStatusCode();

        return (await response.Content
            .ReadFromJsonAsync<FeedbackResponse>(SerializerOptions))!;
    }

    private static async Task ProcessNextAsync(IServiceProvider serviceProvider)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var result = await scope.ServiceProvider
            .GetRequiredService<IFeedbackAnalysisProcessor>()
            .ProcessNextAsync();
        Assert.Equal(FeedbackAnalysisProcessStatus.Succeeded, result.Status);
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(
            JsonNamingPolicy.CamelCase,
            allowIntegerValues: false));
        return options;
    }

    private sealed class DeterministicLlmClient : ILLMClient
    {
        private int _analysisCallCount;
        private int _embeddingCallCount;

        public int AnalysisCallCount => _analysisCallCount;

        public int EmbeddingCallCount => _embeddingCallCount;

        public Task<FeedbackAnalysisResult> AnalyzeFeedbackAsync(
            FeedbackAnalysisRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _analysisCallCount);

            return Task.FromResult(new FeedbackAnalysisResult(
                FeedbackCategory.Bug,
                FeedbackComponent.Payments,
                5,
                FeedbackSentiment.Negative,
                "Customers cannot submit payment cards.",
                "Create an engineering issue for checkout reliability.",
                0.98m));
        }

        public Task<FeedbackEmbeddingResult> GenerateEmbeddingAsync(
            FeedbackEmbeddingRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _embeddingCallCount);

            return Task.FromResult(new FeedbackEmbeddingResult(
                Enumerable.Repeat(0.1f, FeedbackEmbedding.Dimensions).ToArray(),
                "sprint-3-acceptance-model"));
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
            throw new NotSupportedException();
        }
    }

    private sealed class StatisticsAgentTurnClient : IAgentTurnClient
    {
        public List<AgentTurnRequest> Requests { get; } = [];

        public Task<AgentTurnResponse> CreateTurnAsync(
            AgentTurnRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);

            if (request.PreviousToolExchanges.Count == 0)
            {
                return Task.FromResult(new AgentTurnResponse(
                    null,
                    [
                        new AgentToolCall(
                            "acceptance_statistics",
                            AgentToolNames.GetFeedbackStatistics,
                            "{\"periodDays\":7}"),
                    ]));
            }

            var exchange = Assert.Single(request.PreviousToolExchanges);
            using var document = JsonDocument.Parse(exchange.Output.Content);
            var total = document.RootElement
                .GetProperty("totalFeedbackCount")
                .GetInt32();

            return Task.FromResult(new AgentTurnResponse(
                $"Workspace contains {total} feedback items.",
                []));
        }
    }
}
