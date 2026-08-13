#pragma warning disable OPENAI001

using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenAI.Responses;
using PulsePilot.Application;
using PulsePilot.Application.Abstractions.AI;
using PulsePilot.Application.Agents;
using PulsePilot.Application.Common.Exceptions;
using PulsePilot.Infrastructure;
using PulsePilot.Infrastructure.AI;

namespace PulsePilot.IntegrationTests.AI;

public sealed class OpenAIAgentTurnClientTests
{
    private static readonly AgentToolDefinition StatisticsTool = new(
        AgentToolNames.GetFeedbackStatistics,
        "Returns aggregate statistics and a safe error object on failure.",
        """
        {
          "type": "object",
          "properties": {
            "periodDays": {
              "type": ["integer", "null"],
              "minimum": 1,
              "maximum": 365
            }
          },
          "required": ["periodDays"],
          "additionalProperties": false
        }
        """);

    [Fact]
    public async Task CreateTurnAsync_AdvertisesStrictToolsAndReturnsFunctionCalls()
    {
        string? requestBody = null;
        var client = CreateClient(async (request, cancellationToken) =>
        {
            requestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return CreateJsonResponse(CreateResponseJson(
                [
                    CreateReasoningOutput("encrypted-turn-state"),
                    CreateFunctionCallOutput(
                        "call_statistics_1",
                        StatisticsTool.Name,
                        "{\"periodDays\":7}"),
                ]));
        });

        var result = await client.CreateTurnAsync(new AgentTurnRequest(
            "How did feedback change this week?",
            [StatisticsTool],
            []));

        var call = Assert.Single(result.ToolCalls);
        Assert.Null(result.FinalAnswer);
        Assert.Equal("call_statistics_1", call.CallId);
        Assert.Equal(StatisticsTool.Name, call.ToolName);
        Assert.Equal("{\"periodDays\":7}", call.ArgumentsJson);
        var continuationItem = Assert.Single(result.ContinuationItems!);
        Assert.Equal(0, continuationItem.BeforeToolExchangeIndex);
        Assert.Equal("encrypted-turn-state", continuationItem.OpaqueValue);

        Assert.NotNull(requestBody);
        using var requestDocument = JsonDocument.Parse(requestBody);
        var root = requestDocument.RootElement;
        Assert.Equal(OpenAIOptions.DefaultModel, root.GetProperty("model").GetString());
        Assert.False(root.GetProperty("store").GetBoolean());
        Assert.True(root.GetProperty("parallel_tool_calls").GetBoolean());
        Assert.Contains(
            root.GetProperty("include").EnumerateArray(),
            item => item.GetString() == "reasoning.encrypted_content");
        Assert.Contains(
            "untrusted data",
            root.GetProperty("instructions").GetString(),
            StringComparison.Ordinal);

        var tool = Assert.Single(root.GetProperty("tools").EnumerateArray());
        Assert.Equal("function", tool.GetProperty("type").GetString());
        Assert.Equal(StatisticsTool.Name, tool.GetProperty("name").GetString());
        Assert.True(tool.GetProperty("strict").GetBoolean());
        Assert.False(tool.GetProperty("parameters")
            .GetProperty("additionalProperties")
            .GetBoolean());
        Assert.Equal(
            "periodDays",
            tool.GetProperty("parameters")
                .GetProperty("required")[0]
                .GetString());

        var userText = root.GetProperty("input")[0]
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();
        Assert.Equal("How did feedback change this week?", userText);
        Assert.DoesNotContain("integration-test-api-key", requestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateTurnAsync_ReplaysOpaqueReasoningAndToolHistoryInOrder()
    {
        string? requestBody = null;
        var client = CreateClient(async (request, cancellationToken) =>
        {
            requestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return CreateJsonResponse(CreateResponseJson(
                [CreateMessageOutput("Feedback volume increased by 20%.")]));
        });
        var call = new AgentToolCall(
            "call_statistics_1",
            StatisticsTool.Name,
            "{\"periodDays\":7}");

        var result = await client.CreateTurnAsync(new AgentTurnRequest(
            "How did feedback change this week?",
            [StatisticsTool],
            [
                new AgentToolExchange(
                    call,
                    new AgentToolExecutionOutput(
                        true,
                        "{\"totalFeedbackCount\":12}")),
            ],
            [new AgentContinuationItem(0, "encrypted-reasoning-state")]));

        Assert.Equal("Feedback volume increased by 20%.", result.FinalAnswer);
        Assert.Empty(result.ToolCalls);
        Assert.NotNull(requestBody);

        using var requestDocument = JsonDocument.Parse(requestBody);
        var input = requestDocument.RootElement.GetProperty("input");
        Assert.Equal(4, input.GetArrayLength());
        Assert.Equal("message", input[0].GetProperty("type").GetString());
        Assert.Equal("reasoning", input[1].GetProperty("type").GetString());
        Assert.Equal(
            "encrypted-reasoning-state",
            input[1].GetProperty("encrypted_content").GetString());
        Assert.Equal("function_call", input[2].GetProperty("type").GetString());
        Assert.Equal("call_statistics_1", input[2].GetProperty("call_id").GetString());
        Assert.Equal("function_call_output", input[3].GetProperty("type").GetString());
        Assert.Equal("call_statistics_1", input[3].GetProperty("call_id").GetString());
        var functionOutput = input[3].GetProperty("output").GetString();
        Assert.NotNull(functionOutput);
        using var functionOutputDocument = JsonDocument.Parse(functionOutput);
        Assert.True(functionOutputDocument.RootElement
            .GetProperty("succeeded")
            .GetBoolean());
        Assert.Equal(
            12,
            functionOutputDocument.RootElement
                .GetProperty("data")
                .GetProperty("totalFeedbackCount")
                .GetInt32());
    }

    [Fact]
    public async Task CreateTurnAsync_FailsClosedWhenProviderIsDisabled()
    {
        var called = false;
        var client = CreateClient((_, _) =>
        {
            called = true;
            return Task.FromResult(CreateJsonResponse(CreateResponseJson([])));
        }, enabled: false);

        var exception = await Assert.ThrowsAsync<LlmProviderException>(() =>
            client.CreateTurnAsync(new AgentTurnRequest("Question", [], [])));

        Assert.Equal(LlmProviderFailureKind.NotConfigured, exception.FailureKind);
        Assert.False(called);
    }

    [Fact]
    public void Infrastructure_ReplacesFailClosedAgentRuntimeAdapters()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] =
                    "Host=localhost;Database=pulsepilot;Username=pulsepilot",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddApplication();
        services.AddInfrastructure(configuration);
        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        Assert.IsType<OpenAIAgentTurnClient>(
            scope.ServiceProvider.GetRequiredService<IAgentTurnClient>());
        Assert.IsType<AgentToolExecutor>(
            scope.ServiceProvider.GetRequiredService<IAgentToolExecutor>());
    }

    private static OpenAIAgentTurnClient CreateClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory,
        bool enabled = true)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(responseFactory));
        var responsesClient = new ResponsesClient(
            new ApiKeyCredential("integration-test-api-key"),
            new ResponsesClientOptions
            {
                Endpoint = new Uri("https://openai.test/v1/"),
                Transport = new HttpClientPipelineTransport(httpClient),
                RetryPolicy = new ClientRetryPolicy(maxRetries: 0),
                ClientLoggingOptions = new ClientLoggingOptions
                {
                    EnableLogging = false,
                    EnableMessageLogging = false,
                    EnableMessageContentLogging = false,
                },
            });
        var options = Options.Create(new OpenAIOptions
        {
            Enabled = enabled,
            ApiKey = enabled ? "integration-test-api-key" : string.Empty,
        });

        return new OpenAIAgentTurnClient(
            new OpenAIResponseExecutor(responsesClient),
            options);
    }

    private static object CreateFunctionCallOutput(
        string callId,
        string name,
        string arguments)
    {
        return new
        {
            id = $"fc_{callId}",
            type = "function_call",
            status = "completed",
            call_id = callId,
            name,
            arguments,
        };
    }

    private static object CreateReasoningOutput(string encryptedContent)
    {
        return new
        {
            id = "rs_test",
            type = "reasoning",
            status = "completed",
            summary = Array.Empty<object>(),
            encrypted_content = encryptedContent,
        };
    }

    private static object CreateMessageOutput(string text)
    {
        return new
        {
            id = "msg_test",
            type = "message",
            status = "completed",
            role = "assistant",
            content = new[]
            {
                new
                {
                    type = "output_text",
                    text,
                    annotations = Array.Empty<object>(),
                },
            },
        };
    }

    private static string CreateResponseJson(object[] output)
    {
        return JsonSerializer.Serialize(new
        {
            id = "resp_test",
            @object = "response",
            created_at = 1_754_000_000,
            status = "completed",
            background = false,
            error = (object?)null,
            incomplete_details = (object?)null,
            instructions = (string?)null,
            max_output_tokens = 1_000,
            model = OpenAIOptions.DefaultModel,
            output,
            parallel_tool_calls = true,
            previous_response_id = (string?)null,
            reasoning = new
            {
                effort = (string?)null,
                summary = (string?)null,
            },
            store = false,
            temperature = 1,
            text = new { format = new { type = "text" } },
            tool_choice = "auto",
            tools = Array.Empty<object>(),
            top_p = 1,
            truncation = "disabled",
            usage = new
            {
                input_tokens = 10,
                input_tokens_details = new { cached_tokens = 0 },
                output_tokens = 20,
                output_tokens_details = new { reasoning_tokens = 0 },
                total_tokens = 30,
            },
            metadata = new { },
        });
    }

    private static HttpResponseMessage CreateJsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return responseFactory(request, cancellationToken);
        }
    }
}

#pragma warning restore OPENAI001
