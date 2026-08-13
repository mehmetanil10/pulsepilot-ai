using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PulsePilot.Application.Abstractions.AI;
using PulsePilot.Application.Agents;
using PulsePilot.Application.Authentication;
using PulsePilot.Application.Copilot;
using PulsePilot.Application.Feedback;
using PulsePilot.Domain.Feedback;
using PulsePilot.IntegrationTests.Persistence;

namespace PulsePilot.IntegrationTests.Api;

public sealed class CopilotEndpointTests(PostgreSqlFixture database)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly JsonSerializerOptions SerializerOptions =
        CreateSerializerOptions();

    [Fact]
    public async Task Chat_RequiresAuthenticationAndUsesOnlyTokenWorkspace()
    {
        var agentClient = new WorkspaceStatisticsAgentTurnClient();
        await using var factory = new PulsePilotApiFactory(
            database.ConnectionString,
            services =>
            {
                services.RemoveAll<IAgentTurnClient>();
                services.AddSingleton<IAgentTurnClient>(agentClient);
            });
        var owner = await CreateAuthenticatedClientAsync(factory, "copilot-owner");
        var outsider = await CreateAuthenticatedClientAsync(factory, "copilot-outsider");
        using var ownerClient = owner.Client;
        using var outsiderClient = outsider.Client;
        using var anonymousClient = factory.CreateClient();

        using var feedbackResponse = await ownerClient.PostAsync(
            "/api/feedback",
            JsonContent.Create(
                new CreateFeedbackCommand(
                    "Private payment feedback",
                    "A private customer cannot complete checkout.",
                    FeedbackSource.Manual,
                    "Private Customer",
                    "private.customer@example.com"),
                options: SerializerOptions));
        feedbackResponse.EnsureSuccessStatusCode();

        using var ownerResponse = await ownerClient.PostAsJsonAsync(
            "/api/copilot/chat",
            new
            {
                message = "How much feedback arrived this week?",
                workspaceId = outsider.Authentication.WorkspaceId,
            });
        using var outsiderResponse = await outsiderClient.PostAsJsonAsync(
            "/api/copilot/chat",
            new CopilotChatCommand("How much feedback arrived this week?"));
        using var anonymousResponse = await anonymousClient.PostAsJsonAsync(
            "/api/copilot/chat",
            new CopilotChatCommand("How much feedback arrived this week?"));
        using var invalidResponse = await ownerClient.PostAsJsonAsync(
            "/api/copilot/chat",
            new CopilotChatCommand("   "));
        var ownerJson = await ownerResponse.Content.ReadAsStringAsync();
        var ownerResult = JsonSerializer.Deserialize<CopilotChatResponse>(
            ownerJson,
            SerializerOptions);
        var outsiderResult = await outsiderResponse.Content
            .ReadFromJsonAsync<CopilotChatResponse>(SerializerOptions);

        Assert.Equal(HttpStatusCode.OK, ownerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, outsiderResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        Assert.NotNull(ownerResult);
        Assert.NotNull(outsiderResult);
        Assert.Equal("Total feedback: 1.", ownerResult.Answer);
        Assert.Equal("Total feedback: 0.", outsiderResult.Answer);
        Assert.Equal(2, ownerResult.ModelTurnCount);
        Assert.Equal(1, ownerResult.ToolCallCount);
        var usage = Assert.Single(ownerResult.ToolUsages);
        Assert.Equal(AgentToolNames.GetFeedbackStatistics, usage.ToolName);
        Assert.True(usage.Succeeded);
        Assert.DoesNotContain("callId", ownerJson, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(4, agentClient.Requests.Count);
        Assert.All(agentClient.Requests, request =>
        {
            var serialized = JsonSerializer.Serialize(request);
            Assert.DoesNotContain(
                owner.Authentication.WorkspaceId.ToString(),
                serialized,
                StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(
                outsider.Authentication.WorkspaceId.ToString(),
                serialized,
                StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Private Customer", serialized, StringComparison.Ordinal);
            Assert.DoesNotContain(
                "private.customer@example.com",
                serialized,
                StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task Chat_WhenProviderIsDisabled_ReturnsSafeServiceUnavailableProblem()
    {
        await using var factory = new PulsePilotApiFactory(database.ConnectionString);
        var authenticated = await CreateAuthenticatedClientAsync(factory, "copilot-disabled");
        using var client = authenticated.Client;

        using var response = await client.PostAsJsonAsync(
            "/api/copilot/chat",
            new CopilotChatCommand("What changed this week?"));
        var responseBody = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("AI provider unavailable", responseBody, StringComparison.Ordinal);
        Assert.DoesNotContain("not enabled", responseBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("openai", responseBody, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<AuthenticatedClient> CreateAuthenticatedClientAsync(
        PulsePilotApiFactory factory,
        string label)
    {
        var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterCommand(
                $"{label}-{Guid.CreateVersion7():N}@example.com",
                $"{label} owner",
                "correct-horse-battery-staple",
                $"{label} workspace"));
        response.EnsureSuccessStatusCode();
        var authentication = await response.Content
            .ReadFromJsonAsync<AuthenticationResponse>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            authentication!.AccessToken);

        return new AuthenticatedClient(client, authentication);
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(
            JsonNamingPolicy.CamelCase,
            allowIntegerValues: false));

        return options;
    }

    private sealed class WorkspaceStatisticsAgentTurnClient : IAgentTurnClient
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
                            "call_statistics",
                            AgentToolNames.GetFeedbackStatistics,
                            "{\"periodDays\":7}"),
                    ]));
            }

            var exchange = Assert.Single(request.PreviousToolExchanges);
            using var outputDocument = JsonDocument.Parse(exchange.Output.Content);
            var totalFeedback = outputDocument.RootElement
                .GetProperty("totalFeedbackCount")
                .GetInt32();

            return Task.FromResult(new AgentTurnResponse(
                $"Total feedback: {totalFeedback}.",
                []));
        }
    }

    private sealed record AuthenticatedClient(
        HttpClient Client,
        AuthenticationResponse Authentication);
}
