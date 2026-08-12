using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PulsePilot.Application.Abstractions.AI;
using PulsePilot.Application.AI;
using PulsePilot.Application.Authentication;
using PulsePilot.Application.Feedback;
using PulsePilot.Application.FeedbackClusters;
using PulsePilot.Application.FeedbackProcessing;
using PulsePilot.Domain.Feedback;
using PulsePilot.Infrastructure.Persistence;
using PulsePilot.IntegrationTests.Api;
using PulsePilot.IntegrationTests.Persistence;

namespace PulsePilot.IntegrationTests.Acceptance;

public sealed class Sprint2AcceptanceTests(PostgreSqlFixture database)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    [Fact]
    public async Task FeedbackIntelligenceFlow_CompletesSprint2DefinitionOfDone()
    {
        var llmClient = new DeterministicLlmClient();
        await using var factory = new PulsePilotApiFactory(database.ConnectionString)
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ILLMClient>();
                services.AddSingleton<ILLMClient>(llmClient);
            }));
        using var ownerClient = await CreateAuthenticatedClientAsync(factory, "sprint2-owner");
        using var outsiderClient = await CreateAuthenticatedClientAsync(factory, "sprint2-outsider");

        var firstFeedback = await CreateFeedbackAsync(
            ownerClient,
            "Credit card cannot be added",
            "After the latest update I cannot add my credit card.",
            "customer-one@example.com");
        var secondFeedback = await CreateFeedbackAsync(
            ownerClient,
            "Checkout rejects payment cards",
            "The checkout page fails whenever I submit a payment card.",
            "customer-two@example.com");

        Assert.Equal(ProcessingStatus.Pending, firstFeedback.ProcessingStatus);
        Assert.Equal(ProcessingStatus.Pending, secondFeedback.ProcessingStatus);

        var firstProcessing = await ProcessNextAsync(factory.Services);
        var secondProcessing = await ProcessNextAsync(factory.Services);
        var noWork = await ProcessNextAsync(factory.Services);

        Assert.Equal(FeedbackAnalysisProcessStatus.Succeeded, firstProcessing.Status);
        Assert.Equal(FeedbackAnalysisProcessStatus.Succeeded, secondProcessing.Status);
        Assert.Equal(FeedbackAnalysisProcessStatus.NoWork, noWork.Status);
        Assert.Equal(2, llmClient.AnalysisCallCount);
        Assert.Equal(2, llmClient.EmbeddingCallCount);

        var firstCompleted = await ownerClient.GetFromJsonAsync<FeedbackResponse>(
            $"/api/feedback/{firstFeedback.Id}",
            SerializerOptions);
        var secondCompleted = await ownerClient.GetFromJsonAsync<FeedbackResponse>(
            $"/api/feedback/{secondFeedback.Id}",
            SerializerOptions);
        var firstAnalysis = await ownerClient.GetFromJsonAsync<FeedbackAnalysisResponse>(
            $"/api/feedback/{firstFeedback.Id}/analysis",
            SerializerOptions);
        var similar = await ownerClient.GetFromJsonAsync<SimilarFeedbackResponse>(
            $"/api/feedback/{firstFeedback.Id}/similar?limit=10",
            SerializerOptions);
        var clusters = await ownerClient.GetFromJsonAsync<FeedbackClusterListResponse>(
            "/api/clusters",
            SerializerOptions);

        Assert.NotNull(firstCompleted);
        Assert.NotNull(secondCompleted);
        Assert.Equal(ProcessingStatus.Completed, firstCompleted.ProcessingStatus);
        Assert.Equal(ProcessingStatus.Completed, secondCompleted.ProcessingStatus);
        Assert.NotNull(firstCompleted.FeedbackClusterId);
        Assert.Equal(firstCompleted.FeedbackClusterId, secondCompleted.FeedbackClusterId);

        Assert.NotNull(firstAnalysis);
        Assert.True(firstAnalysis.IsCurrent);
        Assert.Equal(ProcessingStatus.Completed, firstAnalysis.ProcessingStatus);
        Assert.NotNull(firstAnalysis.Analysis);
        Assert.Equal(FeedbackCategory.Bug, firstAnalysis.Analysis.Category);
        Assert.Equal(FeedbackComponent.Payments, firstAnalysis.Analysis.Component);
        Assert.Equal(5, firstAnalysis.Analysis.Severity);
        Assert.Equal(FeedbackSentiment.Negative, firstAnalysis.Analysis.Sentiment);

        Assert.NotNull(similar);
        var similarFeedback = Assert.Single(similar.Items);
        Assert.Equal(secondFeedback.Id, similarFeedback.Id);
        Assert.Equal(firstCompleted.FeedbackClusterId, similarFeedback.FeedbackClusterId);
        Assert.True(similarFeedback.Similarity > 0.99);

        Assert.NotNull(clusters);
        var cluster = Assert.Single(clusters.Items);
        Assert.Equal(firstCompleted.FeedbackClusterId, cluster.Id);
        Assert.Equal(2, cluster.FeedbackCount);
        Assert.Equal(57m, cluster.PriorityScore);
        Assert.Equal(FeedbackPriority.P2, cluster.Priority);

        var clusterDetail = await ownerClient.GetFromJsonAsync<FeedbackClusterDetailResponse>(
            $"/api/clusters/{cluster.Id}",
            SerializerOptions);
        var outsiderClusters = await outsiderClient.GetFromJsonAsync<FeedbackClusterListResponse>(
            "/api/clusters",
            SerializerOptions);
        using var outsiderDetail = await outsiderClient.GetAsync($"/api/clusters/{cluster.Id}");

        Assert.NotNull(clusterDetail);
        Assert.Equal(2, clusterDetail.TotalFeedbackCount);
        Assert.Equal(2, clusterDetail.Feedback.Count);
        Assert.Equal(57m, clusterDetail.PriorityScore);
        Assert.NotNull(outsiderClusters);
        Assert.Empty(outsiderClusters.Items);
        Assert.Equal(HttpStatusCode.NotFound, outsiderDetail.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(2, await dbContext.FeedbackAnalyses.CountAsync(
            analysis => analysis.WorkspaceId == firstCompleted.WorkspaceId));
        Assert.Equal(2, await dbContext.FeedbackEmbeddings.CountAsync(
            embedding => embedding.WorkspaceId == firstCompleted.WorkspaceId));
        Assert.Equal(1, await dbContext.FeedbackClusters.CountAsync(
            persistedCluster => persistedCluster.WorkspaceId == firstCompleted.WorkspaceId));
    }

    private static async Task<FeedbackResponse> CreateFeedbackAsync(
        HttpClient client,
        string title,
        string content,
        string customerEmail)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/feedback",
            new CreateFeedbackCommand(
                title,
                content,
                FeedbackSource.Api,
                null,
                customerEmail),
            SerializerOptions);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<FeedbackResponse>(SerializerOptions))!;
    }

    private static async Task<HttpClient> CreateAuthenticatedClientAsync(
        WebApplicationFactory<Program> factory,
        string label)
    {
        var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterCommand(
                $"{label}-{Guid.CreateVersion7():N}@example.com",
                $"{label} user",
                "correct-horse-battery-staple",
                $"{label} workspace"),
            SerializerOptions);
        response.EnsureSuccessStatusCode();
        var authentication = await response.Content.ReadFromJsonAsync<AuthenticationResponse>(
            SerializerOptions);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            authentication!.AccessToken);

        return client;
    }

    private static async Task<FeedbackAnalysisProcessResult> ProcessNextAsync(
        IServiceProvider serviceProvider)
    {
        await using var scope = serviceProvider.CreateAsyncScope();

        return await scope.ServiceProvider
            .GetRequiredService<IFeedbackAnalysisProcessor>()
            .ProcessNextAsync();
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));

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
                "Inspect payment tokenization and add a regression test.",
                0.96m));
        }

        public Task<FeedbackEmbeddingResult> GenerateEmbeddingAsync(
            FeedbackEmbeddingRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _embeddingCallCount);

            return Task.FromResult(new FeedbackEmbeddingResult(
                Enumerable.Repeat(0.1f, FeedbackEmbedding.Dimensions).ToArray(),
                "sprint-2-acceptance-model"));
        }
    }
}
