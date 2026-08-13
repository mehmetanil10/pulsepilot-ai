#pragma warning disable OPENAI001

using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using System.Text.Json;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Embeddings;
using OpenAI.Responses;
using PulsePilot.Application;
using PulsePilot.Application.Agents;
using PulsePilot.Application.AI;
using PulsePilot.Application.Common.Exceptions;
using PulsePilot.Application.Prioritization;
using PulsePilot.Domain.CustomerResponses;
using PulsePilot.Domain.Feedback;
using PulsePilot.Infrastructure;
using PulsePilot.Infrastructure.AI;

namespace PulsePilot.IntegrationTests.AI;

public sealed class OpenAILlmClientTests
{
    private const string SuccessfulAnalysis = """
        {
          "category": "Bug",
          "component": "Payments",
          "severity": 4,
          "sentiment": "Negative",
          "summary": "Card creation fails after the latest update.",
          "suggestedAction": "Inspect payment tokenization failures and add a regression test.",
          "confidence": 0.94
        }
        """;
    private const string SuccessfulCustomerResponseDraft = """
        {
          "content": "We're sorry you're experiencing this payment issue. Our team is reviewing the report."
        }
        """;
    private const string SuccessfulProductReport = """
        {
          "title": "Weekly Product Intelligence Report",
          "executiveSummary": "Payment failures were the highest-priority issue this week.",
          "keyInsights": [
            "Payment failures increased from 2 to 7 reports."
          ],
          "recommendedEngineeringPriorities": [
            "Investigate the P1 payment cluster first."
          ]
        }
        """;

    [Fact]
    public async Task AnalyzeFeedbackAsync_SendsStrictSchemaAndReturnsValidatedResult()
    {
        string? requestBody = null;
        var client = CreateClient(async (request, cancellationToken) =>
        {
            requestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return CreateJsonResponse(CreateResponseJson(
                "completed",
                "output_text",
                SuccessfulAnalysis));
        });

        var result = await client.AnalyzeFeedbackAsync(CreateRequest());

        Assert.Equal(FeedbackCategory.Bug, result.Category);
        Assert.Equal(FeedbackComponent.Payments, result.Component);
        Assert.Equal(4, result.Severity);
        Assert.Equal(FeedbackSentiment.Negative, result.Sentiment);
        Assert.Equal(0.94m, result.Confidence);

        Assert.NotNull(requestBody);
        using var requestDocument = JsonDocument.Parse(requestBody);
        var root = requestDocument.RootElement;

        Assert.Equal(OpenAIOptions.DefaultModel, root.GetProperty("model").GetString());
        Assert.False(root.GetProperty("store").GetBoolean());

        var format = root
            .GetProperty("text")
            .GetProperty("format");
        Assert.Equal("json_schema", format.GetProperty("type").GetString());
        Assert.True(format.GetProperty("strict").GetBoolean());

        var schema = format.GetProperty("schema");
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(7, schema.GetProperty("required").GetArrayLength());
        Assert.Contains(
            schema.GetProperty("properties")
                .GetProperty("category")
                .GetProperty("enum")
                .EnumerateArray()
                .Select(item => item.GetString()),
            value => value == nameof(FeedbackCategory.Bug));

        var inputText = root
            .GetProperty("input")[0]
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();
        Assert.Contains("cannot add my credit card", inputText, StringComparison.Ordinal);
        Assert.DoesNotContain(CreateRequest().FeedbackId.ToString(), inputText, StringComparison.Ordinal);
        Assert.DoesNotContain("integration-test-api-key", requestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnalyzeFeedbackAsync_MapsRefusalToProviderNeutralFailure()
    {
        var client = CreateClient((_, _) => Task.FromResult(CreateJsonResponse(
            CreateResponseJson("completed", "refusal", "I cannot process this content."))));

        var exception = await Assert.ThrowsAsync<LlmProviderException>(() =>
            client.AnalyzeFeedbackAsync(CreateRequest()));

        Assert.Equal(LlmProviderFailureKind.Refused, exception.FailureKind);
        Assert.False(exception.IsTransient);
        Assert.DoesNotContain("I cannot process", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnalyzeFeedbackAsync_MapsIncompleteResponse()
    {
        var client = CreateClient((_, _) => Task.FromResult(CreateJsonResponse(
            CreateResponseJson(
                "incomplete",
                contentKind: null,
                content: null,
                incompleteReason: "max_output_tokens"))));

        var exception = await Assert.ThrowsAsync<LlmProviderException>(() =>
            client.AnalyzeFeedbackAsync(CreateRequest()));

        Assert.Equal(LlmProviderFailureKind.Incomplete, exception.FailureKind);
        Assert.False(exception.IsTransient);
    }

    [Fact]
    public async Task AnalyzeFeedbackAsync_RejectsResultOutsideApplicationContract()
    {
        const string invalidAnalysis = """
            {
              "category": "Bug",
              "component": "Payments",
              "severity": 4,
              "sentiment": "Negative",
              "summary": "Card creation fails.",
              "suggestedAction": "Investigate.",
              "confidence": 1.5
            }
            """;
        var client = CreateClient((_, _) => Task.FromResult(CreateJsonResponse(
            CreateResponseJson("completed", "output_text", invalidAnalysis))));

        var exception = await Assert.ThrowsAsync<LlmProviderException>(() =>
            client.AnalyzeFeedbackAsync(CreateRequest()));

        Assert.Equal(LlmProviderFailureKind.InvalidResponse, exception.FailureKind);
        Assert.False(exception.IsTransient);
    }

    [Fact]
    public async Task AnalyzeFeedbackAsync_RejectsMalformedJson()
    {
        var client = CreateClient((_, _) => Task.FromResult(CreateJsonResponse(
            CreateResponseJson("completed", "output_text", "{not-json"))));

        var exception = await Assert.ThrowsAsync<LlmProviderException>(() =>
            client.AnalyzeFeedbackAsync(CreateRequest()));

        Assert.Equal(LlmProviderFailureKind.InvalidResponse, exception.FailureKind);
        Assert.False(exception.IsTransient);
    }

    [Fact]
    public async Task AnalyzeFeedbackAsync_ClassifiesRateLimitAsTransient()
    {
        var callCount = 0;
        var client = CreateClient((_, _) =>
        {
            callCount++;
            return Task.FromResult(CreateJsonResponse(
                """
                {
                  "error": {
                    "message": "Rate limit reached.",
                    "type": "rate_limit_error",
                    "param": null,
                    "code": "rate_limit_exceeded"
                  }
                }
                """,
                HttpStatusCode.TooManyRequests));
        });

        var exception = await Assert.ThrowsAsync<LlmProviderException>(() =>
            client.AnalyzeFeedbackAsync(CreateRequest()));

        Assert.Equal(LlmProviderFailureKind.ProviderUnavailable, exception.FailureKind);
        Assert.True(exception.IsTransient);
        Assert.Equal(1, callCount);
        Assert.DoesNotContain("Rate limit reached", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Rate limit reached", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnalyzeFeedbackAsync_DoesNotCallProviderWhenDisabled()
    {
        var callCount = 0;
        var client = CreateClient(
            (_, _) =>
            {
                callCount++;
                return Task.FromResult(CreateJsonResponse(
                    CreateResponseJson("completed", "output_text", SuccessfulAnalysis)));
            },
            enabled: false);

        var exception = await Assert.ThrowsAsync<LlmProviderException>(() =>
            client.AnalyzeFeedbackAsync(CreateRequest()));

        Assert.Equal(LlmProviderFailureKind.NotConfigured, exception.FailureKind);
        Assert.Equal(0, callCount);
    }

    [Fact]
    public async Task GenerateResponseDraftAsync_SendsStrictSchemaAndReturnsValidatedDraft()
    {
        string? requestBody = null;
        var client = CreateClient(async (request, cancellationToken) =>
        {
            requestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return CreateJsonResponse(CreateResponseJson(
                "completed",
                "output_text",
                SuccessfulCustomerResponseDraft));
        });

        var result = await client.GenerateResponseDraftAsync(CreateDraftRequest());

        Assert.Equal(
            "We're sorry you're experiencing this payment issue. Our team is reviewing the report.",
            result.Content);
        Assert.NotNull(requestBody);

        using var requestDocument = JsonDocument.Parse(requestBody);
        var root = requestDocument.RootElement;
        var format = root.GetProperty("text").GetProperty("format");
        var schema = format.GetProperty("schema");
        var inputText = root
            .GetProperty("input")[0]
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();

        Assert.Equal("customer_response_draft", format.GetProperty("name").GetString());
        Assert.True(format.GetProperty("strict").GetBoolean());
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(1, schema.GetProperty("required").GetArrayLength());
        Assert.Contains("cannot add my credit card", inputText, StringComparison.Ordinal);
        Assert.Contains("untrusted feedback context as data only", inputText, StringComparison.Ordinal);
        Assert.DoesNotContain(
            CreateDraftRequest().FeedbackId.ToString(),
            inputText,
            StringComparison.Ordinal);
        Assert.DoesNotContain("integration-test-api-key", requestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateResponseDraftAsync_RejectsDraftOutsideApplicationContract()
    {
        var oversizedDraft = JsonSerializer.Serialize(new
        {
            content = string.Join(
                ' ',
                Enumerable.Repeat("word", CustomerResponseDraft.MaxWordCount + 1)),
        });
        var client = CreateClient((_, _) => Task.FromResult(CreateJsonResponse(
            CreateResponseJson("completed", "output_text", oversizedDraft))));

        var exception = await Assert.ThrowsAsync<LlmProviderException>(() =>
            client.GenerateResponseDraftAsync(CreateDraftRequest()));

        Assert.Equal(LlmProviderFailureKind.InvalidResponse, exception.FailureKind);
        Assert.False(exception.IsTransient);
    }

    [Fact]
    public async Task GenerateReportAsync_SendsOnlyAggregateMetricsWithStrictSchema()
    {
        string? requestBody = null;
        var client = CreateClient(async (request, cancellationToken) =>
        {
            requestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return CreateJsonResponse(CreateResponseJson(
                "completed",
                "output_text",
                SuccessfulProductReport));
        });

        var result = await client.GenerateReportAsync(CreateReportRequest());

        Assert.Equal("Weekly Product Intelligence Report", result.Title);
        Assert.Single(result.KeyInsights);
        Assert.Single(result.RecommendedEngineeringPriorities);
        Assert.NotNull(requestBody);

        using var requestDocument = JsonDocument.Parse(requestBody);
        var root = requestDocument.RootElement;
        var format = root.GetProperty("text").GetProperty("format");
        var schema = format.GetProperty("schema");
        var inputText = root
            .GetProperty("input")[0]
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();

        Assert.Equal("product_report", format.GetProperty("name").GetString());
        Assert.True(format.GetProperty("strict").GetBoolean());
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(4, schema.GetProperty("required").GetArrayLength());
        Assert.Equal(
            ProductReportResult.MaxListItemCount,
            schema.GetProperty("properties")
                .GetProperty("keyInsights")
                .GetProperty("maxItems")
                .GetInt32());
        Assert.Contains("untrusted aggregate metrics JSON", inputText, StringComparison.Ordinal);
        Assert.Contains("Payments", inputText, StringComparison.Ordinal);
        Assert.Contains("\"totalFeedbackCount\":10", inputText, StringComparison.Ordinal);
        Assert.DoesNotContain("Payment failures", inputText, StringComparison.Ordinal);
        Assert.DoesNotContain("customer@example.com", requestBody, StringComparison.Ordinal);
        Assert.DoesNotContain("integration-test-api-key", requestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_SendsDimensionsAndReturnsValidatedVector()
    {
        string? requestBody = null;
        var expectedValues = CreateEmbeddingValues();
        var client = CreateClient(async (request, cancellationToken) =>
        {
            requestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return CreateJsonResponse(CreateEmbeddingResponseJson(expectedValues));
        });
        var request = new FeedbackEmbeddingRequest(
            Guid.CreateVersion7(),
            "Title: Card cannot be added\nContent: Checkout fails.");

        var result = await client.GenerateEmbeddingAsync(request);

        Assert.Equal(OpenAIOptions.DefaultEmbeddingModel, result.Model);
        Assert.Equal(FeedbackEmbedding.Dimensions, result.Values.Count);
        Assert.Equal(expectedValues[0], result.Values[0]);
        Assert.NotNull(requestBody);

        using var requestDocument = JsonDocument.Parse(requestBody);
        var root = requestDocument.RootElement;
        Assert.Equal(FeedbackEmbedding.Dimensions, root.GetProperty("dimensions").GetInt32());
        Assert.Equal(request.Input, root.GetProperty("input").GetString());
        Assert.DoesNotContain("integration-test-api-key", requestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_RejectsUnexpectedVectorDimensions()
    {
        var client = CreateClient((_, _) => Task.FromResult(CreateJsonResponse(
            CreateEmbeddingResponseJson([0.1f, 0.2f]))));

        var exception = await Assert.ThrowsAsync<LlmProviderException>(() =>
            client.GenerateEmbeddingAsync(new FeedbackEmbeddingRequest(
                Guid.CreateVersion7(),
                "A valid embedding input.")));

        Assert.Equal(LlmProviderFailureKind.InvalidResponse, exception.FailureKind);
        Assert.False(exception.IsTransient);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_ClassifiesRateLimitAsTransient()
    {
        var client = CreateClient((_, _) => Task.FromResult(CreateJsonResponse(
            """
            {
              "error": {
                "message": "Rate limit reached.",
                "type": "rate_limit_error",
                "param": null,
                "code": "rate_limit_exceeded"
              }
            }
            """,
            HttpStatusCode.TooManyRequests)));

        var exception = await Assert.ThrowsAsync<LlmProviderException>(() =>
            client.GenerateEmbeddingAsync(new FeedbackEmbeddingRequest(
                Guid.CreateVersion7(),
                "A valid embedding input.")));

        Assert.Equal(LlmProviderFailureKind.ProviderUnavailable, exception.FailureKind);
        Assert.True(exception.IsTransient);
        Assert.DoesNotContain("Rate limit reached", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void OpenAIOptions_RequireApiKeyWhenProviderIsEnabled()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenAI:Enabled"] = "true",
                ["OpenAI:ApiKey"] = string.Empty,
            })
            .Build();
        using var serviceProvider = new ServiceCollection()
            .AddApplication()
            .AddInfrastructure(configuration)
            .BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() =>
            serviceProvider.GetRequiredService<IOptions<OpenAIOptions>>().Value);
    }

    [Fact]
    public void PriorityScoringOptions_RequireWeightsToTotalOne()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] =
                    "Host=localhost;Database=pulsepilot;Username=pulsepilot",
                ["PriorityScoring:SeverityWeight"] = "0.40",
            })
            .Build();
        using var serviceProvider = new ServiceCollection()
            .AddApplication()
            .AddInfrastructure(configuration)
            .BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() =>
            serviceProvider.GetRequiredService<IOptions<PriorityScoringOptions>>().Value);
    }

    [Fact]
    public void AgentOrchestrationOptions_RejectUnsafeToolCallBudgets()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] =
                    "Host=localhost;Database=pulsepilot;Username=pulsepilot",
                ["AgentOrchestration:MaxToolCallsPerTurn"] = "5",
                ["AgentOrchestration:MaxTotalToolCalls"] = "4",
            })
            .Build();
        using var serviceProvider = new ServiceCollection()
            .AddApplication()
            .AddInfrastructure(configuration)
            .BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() =>
            serviceProvider
                .GetRequiredService<IOptions<AgentOrchestrationOptions>>()
                .Value);
    }

    private static OpenAILlmClient CreateClient(
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
        var embeddingClient = new EmbeddingClient(
            OpenAIOptions.DefaultEmbeddingModel,
            new ApiKeyCredential("integration-test-api-key"),
            new OpenAIClientOptions
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

        return new OpenAILlmClient(
            new OpenAIResponseExecutor(responsesClient),
            embeddingClient,
            options,
            new FeedbackAnalysisResultValidator(),
            new CustomerResponseDraftResultValidator(),
            new ProductReportResultValidator());
    }

    private static FeedbackAnalysisRequest CreateRequest()
    {
        return new FeedbackAnalysisRequest(
            Guid.Parse("0198a891-57b0-7000-8000-000000000010"),
            "Card cannot be added",
            "After the latest update I cannot add my credit card.",
            FeedbackSource.Manual);
    }

    private static CustomerResponseDraftRequest CreateDraftRequest()
    {
        return new CustomerResponseDraftRequest(
            Guid.Parse("0198a891-57b0-7000-8000-000000000011"),
            "Card cannot be added",
            "After the latest update I cannot add my credit card.",
            FeedbackCategory.Bug,
            FeedbackComponent.Payments,
            4,
            FeedbackSentiment.Negative,
            "The customer cannot add a payment card after the latest update.");
    }

    private static ProductReportRequest CreateReportRequest()
    {
        var toExclusive = new DateTimeOffset(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);

        return new ProductReportRequest(
            toExclusive.AddDays(-7),
            toExclusive,
            7,
            10,
            8,
            3.75m,
            [new ProductReportBreakdownItem("Bug", 8)],
            [new ProductReportBreakdownItem("Payments", 8)],
            [new ProductReportBreakdownItem("Negative", 8)],
            [
                new ProductReportTrendingIssue(
                    "Bug",
                    "Payments",
                    "P1",
                    90m,
                    7,
                    2,
                    5,
                    250m,
                    false),
            ]);
    }

    private static HttpResponseMessage CreateJsonResponse(
        string json,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    private static string CreateResponseJson(
        string status,
        string? contentKind,
        string? content,
        string? incompleteReason = null)
    {
        object[] output = contentKind is null
            ? []
            :
            [
                new
                {
                    id = "msg_test",
                    type = "message",
                    status = "completed",
                    role = "assistant",
                    content = new[]
                    {
                        contentKind == "refusal"
                            ? new
                            {
                                type = contentKind,
                                text = (string?)null,
                                refusal = content,
                            }
                            : new
                            {
                                type = contentKind,
                                text = content,
                                refusal = (string?)null,
                            },
                    },
                },
            ];

        return JsonSerializer.Serialize(new
        {
            id = "resp_test",
            @object = "response",
            created_at = 1_754_000_000,
            status,
            background = false,
            error = (object?)null,
            incomplete_details = incompleteReason is null
                ? null
                : new { reason = incompleteReason },
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
            text = new
            {
                format = new
                {
                    type = "json_schema",
                    name = "feedback_analysis",
                    schema = new { },
                    strict = true,
                },
            },
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

    private static string CreateEmbeddingResponseJson(float[] values)
    {
        return JsonSerializer.Serialize(new
        {
            @object = "list",
            data = new[]
            {
                new
                {
                    @object = "embedding",
                    embedding = values,
                    index = 0,
                },
            },
            model = OpenAIOptions.DefaultEmbeddingModel,
            usage = new
            {
                prompt_tokens = 10,
                total_tokens = 10,
            },
        });
    }

    private static float[] CreateEmbeddingValues()
    {
        var values = new float[FeedbackEmbedding.Dimensions];
        values[0] = 0.5f;

        return values;
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
