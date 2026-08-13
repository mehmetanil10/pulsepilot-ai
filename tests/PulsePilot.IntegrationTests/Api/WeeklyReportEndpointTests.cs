using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PulsePilot.Application.Abstractions.AI;
using PulsePilot.Application.AI;
using PulsePilot.Application.Authentication;
using PulsePilot.Application.Feedback;
using PulsePilot.Application.Reports;
using PulsePilot.Application.Tools;
using PulsePilot.Domain.Feedback;
using PulsePilot.IntegrationTests.Persistence;

namespace PulsePilot.IntegrationTests.Api;

public sealed class WeeklyReportEndpointTests(PostgreSqlFixture database)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly JsonSerializerOptions SerializerOptions =
        CreateSerializerOptions();

    [Fact]
    public async Task GenerateWeekly_RequiresAuthenticationAndUsesTokenWorkspaceOnly()
    {
        var llmClient = new ReportLlmClient();
        await using var factory = new PulsePilotApiFactory(
            database.ConnectionString,
            services =>
            {
                services.RemoveAll<ILLMClient>();
                services.AddSingleton<ILLMClient>(llmClient);
            });
        using var ownerClient = await CreateAuthenticatedClientAsync(factory, "report-owner");
        using var outsiderClient = await CreateAuthenticatedClientAsync(
            factory,
            "report-outsider");
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
            "/api/reports/weekly",
            new GenerateWeeklyReportCommand());
        using var outsiderResponse = await outsiderClient.PostAsJsonAsync(
            "/api/reports/weekly",
            new GenerateWeeklyReportCommand());
        using var anonymousResponse = await anonymousClient.PostAsJsonAsync(
            "/api/reports/weekly",
            new GenerateWeeklyReportCommand());
        using var invalidResponse = await ownerClient.PostAsJsonAsync(
            "/api/reports/weekly",
            new GenerateWeeklyReportCommand(PeriodDays: 0));
        var ownerReport = await ownerResponse.Content
            .ReadFromJsonAsync<GenerateReportToolResult>(SerializerOptions);
        var outsiderReport = await outsiderResponse.Content
            .ReadFromJsonAsync<GenerateReportToolResult>(SerializerOptions);

        Assert.Equal(HttpStatusCode.OK, ownerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, outsiderResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        Assert.NotNull(ownerReport);
        Assert.NotNull(outsiderReport);
        Assert.Equal(1, ownerReport.Statistics.TotalFeedbackCount);
        Assert.Equal(0, outsiderReport.Statistics.TotalFeedbackCount);
        Assert.Equal(2, llmClient.Requests.Count);
        Assert.Equal(
            [1, 0],
            llmClient.Requests.Select(request => request.TotalFeedbackCount));
        Assert.All(llmClient.Requests, request =>
        {
            var serialized = JsonSerializer.Serialize(request);
            Assert.DoesNotContain("Private Customer", serialized, StringComparison.Ordinal);
            Assert.DoesNotContain(
                "private.customer@example.com",
                serialized,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "private customer cannot complete checkout",
                serialized,
                StringComparison.OrdinalIgnoreCase);
        });
    }

    private static async Task<HttpClient> CreateAuthenticatedClientAsync(
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

        return client;
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));

        return options;
    }

    private sealed class ReportLlmClient : ILLMClient
    {
        private readonly List<ProductReportRequest> _requests = [];

        public IReadOnlyList<ProductReportRequest> Requests => _requests;

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
            _requests.Add(request);

            return Task.FromResult(new ProductReportResult(
                "Weekly Product Intelligence Report",
                request.TotalFeedbackCount == 0
                    ? "No feedback was received during the selected period."
                    : $"{request.TotalFeedbackCount} feedback record was received.",
                ["The report uses workspace-scoped aggregate metrics."],
                ["Continue monitoring feedback trends."]));
        }
    }
}
