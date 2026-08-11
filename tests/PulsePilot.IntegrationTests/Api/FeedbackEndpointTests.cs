using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PulsePilot.Application.AI;
using PulsePilot.Application.Authentication;
using PulsePilot.Application.Feedback;
using PulsePilot.Domain.Feedback;
using PulsePilot.Infrastructure.Persistence;
using PulsePilot.IntegrationTests.Persistence;

namespace PulsePilot.IntegrationTests.Api;

public sealed class FeedbackEndpointTests(PostgreSqlFixture database)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    [Fact]
    public async Task CrudFlow_EnforcesWorkspaceIsolationAndSoftDelete()
    {
        await using var factory = new PulsePilotApiFactory(database.ConnectionString);
        using var ownerClient = await CreateAuthenticatedClientAsync(factory, "owner");
        using var outsiderClient = await CreateAuthenticatedClientAsync(factory, "outsider");
        var createCommand = new CreateFeedbackCommand(
            "Payment problem",
            "I cannot add my card after the latest update.",
            FeedbackSource.Manual,
            "Example Customer",
            "customer@example.com");

        using var createResponse = await PostAsJsonAsync(
            ownerClient,
            "/api/feedback",
            createCommand);
        var created = await ReadAsync<FeedbackResponse>(createResponse);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.Equal(ProcessingStatus.Pending, created.ProcessingStatus);
        Assert.Equal(FeedbackSource.Manual, created.Source);
        Assert.NotNull(createResponse.Headers.Location);

        var pendingAnalysis = await ownerClient.GetFromJsonAsync<FeedbackAnalysisResponse>(
            $"/api/feedback/{created.Id}/analysis",
            SerializerOptions);
        using var retryPendingResponse = await ownerClient.PostAsync(
            $"/api/feedback/{created.Id}/analysis/retry",
            content: null);
        using var pendingSimilarResponse = await ownerClient.GetAsync(
            $"/api/feedback/{created.Id}/similar");

        Assert.NotNull(pendingAnalysis);
        Assert.Equal(ProcessingStatus.Pending, pendingAnalysis.ProcessingStatus);
        Assert.False(pendingAnalysis.IsCurrent);
        Assert.Null(pendingAnalysis.Analysis);
        Assert.Equal(HttpStatusCode.Conflict, retryPendingResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, pendingSimilarResponse.StatusCode);

        using var outsiderGetResponse = await outsiderClient.GetAsync(
            $"/api/feedback/{created.Id}");
        using var outsiderAnalysisResponse = await outsiderClient.GetAsync(
            $"/api/feedback/{created.Id}/analysis");
        using var outsiderSimilarResponse = await outsiderClient.GetAsync(
            $"/api/feedback/{created.Id}/similar");
        using var outsiderDeleteResponse = await outsiderClient.DeleteAsync(
            $"/api/feedback/{created.Id}");

        Assert.Equal(HttpStatusCode.NotFound, outsiderGetResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, outsiderAnalysisResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, outsiderSimilarResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, outsiderDeleteResponse.StatusCode);

        await CompleteAnalysisAsync(factory, created.Id);
        var completedAnalysis = await ownerClient.GetFromJsonAsync<FeedbackAnalysisResponse>(
            $"/api/feedback/{created.Id}/analysis",
            SerializerOptions);

        Assert.NotNull(completedAnalysis);
        Assert.Equal(ProcessingStatus.Completed, completedAnalysis.ProcessingStatus);
        Assert.True(completedAnalysis.IsCurrent);
        Assert.NotNull(completedAnalysis.Analysis);
        Assert.Equal(FeedbackCategory.Bug, completedAnalysis.Analysis.Category);
        Assert.Equal(FeedbackComponent.Payments, completedAnalysis.Analysis.Component);

        var updateCommand = new UpdateFeedbackCommand(
            "Payment card problem",
            "Card creation fails after the latest update.",
            FeedbackSource.Api,
            "Example Customer",
            "customer@example.com");
        using var updateResponse = await PutAsJsonAsync(
            ownerClient,
            $"/api/feedback/{created.Id}",
            updateCommand);
        var updated = await ReadAsync<FeedbackResponse>(updateResponse);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.NotNull(updated);
        Assert.Equal("Payment card problem", updated.Title);
        Assert.Equal(FeedbackSource.Api, updated.Source);
        Assert.Equal(ProcessingStatus.Pending, updated.ProcessingStatus);

        var staleAnalysis = await ownerClient.GetFromJsonAsync<FeedbackAnalysisResponse>(
            $"/api/feedback/{created.Id}/analysis",
            SerializerOptions);

        Assert.NotNull(staleAnalysis);
        Assert.Equal(ProcessingStatus.Pending, staleAnalysis.ProcessingStatus);
        Assert.False(staleAnalysis.IsCurrent);
        Assert.NotNull(staleAnalysis.Analysis);

        await MarkAnalysisFailedAsync(factory, created.Id);
        using var retryFailedResponse = await ownerClient.PostAsync(
            $"/api/feedback/{created.Id}/analysis/retry",
            content: null);
        var retried = await ReadAsync<FeedbackResponse>(retryFailedResponse);

        Assert.Equal(HttpStatusCode.OK, retryFailedResponse.StatusCode);
        Assert.NotNull(retried);
        Assert.Equal(ProcessingStatus.Pending, retried.ProcessingStatus);

        using var getResponse = await ownerClient.GetAsync($"/api/feedback/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        using var deleteResponse = await ownerClient.DeleteAsync(
            $"/api/feedback/{created.Id}");
        using var deletedGetResponse = await ownerClient.GetAsync(
            $"/api/feedback/{created.Id}");
        var listAfterDelete = await ownerClient.GetFromJsonAsync<FeedbackListResponse>(
            "/api/feedback",
            SerializerOptions);

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deletedGetResponse.StatusCode);
        Assert.NotNull(listAfterDelete);
        Assert.Equal(0, listAfterDelete.TotalCount);

        await using var serviceProvider = database.CreateServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();
        var persistedFeedback = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Feedback
            .IgnoreQueryFilters()
            .SingleAsync(feedback => feedback.Id == created.Id);

        Assert.True(persistedFeedback.IsDeleted);
        Assert.NotNull(persistedFeedback.DeletedAt);
    }

    [Fact]
    public async Task List_AppliesSourceStatusAndPaginationFilters()
    {
        await using var factory = new PulsePilotApiFactory(database.ConnectionString);
        using var client = await CreateAuthenticatedClientAsync(factory, "filters");

        await CreateFeedbackAsync(client, "Manual one", FeedbackSource.Manual);
        await CreateFeedbackAsync(client, "Manual two", FeedbackSource.Manual);
        await CreateFeedbackAsync(client, "API one", FeedbackSource.Api);

        var firstPage = await client.GetFromJsonAsync<FeedbackListResponse>(
            "/api/feedback?source=manual&page=1&pageSize=1",
            SerializerOptions);
        var secondPage = await client.GetFromJsonAsync<FeedbackListResponse>(
            "/api/feedback?source=manual&page=2&pageSize=1",
            SerializerOptions);
        var pending = await client.GetFromJsonAsync<FeedbackListResponse>(
            "/api/feedback?processingStatus=pending&pageSize=10",
            SerializerOptions);

        Assert.NotNull(firstPage);
        Assert.Equal(2, firstPage.TotalCount);
        Assert.Single(firstPage.Items);
        Assert.All(firstPage.Items, item => Assert.Equal(FeedbackSource.Manual, item.Source));

        Assert.NotNull(secondPage);
        Assert.Equal(2, secondPage.TotalCount);
        Assert.Single(secondPage.Items);
        Assert.NotEqual(firstPage.Items[0].Id, secondPage.Items[0].Id);

        Assert.NotNull(pending);
        Assert.Equal(3, pending.TotalCount);
        Assert.All(
            pending.Items,
            item => Assert.Equal(ProcessingStatus.Pending, item.ProcessingStatus));
    }

    [Fact]
    public async Task Create_RequiresAuthenticationAndValidContent()
    {
        await using var factory = new PulsePilotApiFactory(database.ConnectionString);
        using var anonymousClient = factory.CreateClient();
        using var authenticatedClient = await CreateAuthenticatedClientAsync(factory, "validation");
        var invalidCommand = new CreateFeedbackCommand(
            null,
            string.Empty,
            FeedbackSource.Manual,
            null,
            "invalid-email");

        using var unauthorizedResponse = await PostAsJsonAsync(
            anonymousClient,
            "/api/feedback",
            invalidCommand);
        using var validationResponse = await PostAsJsonAsync(
            authenticatedClient,
            "/api/feedback",
            invalidCommand);

        Assert.Equal(HttpStatusCode.Unauthorized, unauthorizedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, validationResponse.StatusCode);

        using var document = JsonDocument.Parse(
            await validationResponse.Content.ReadAsStreamAsync());
        var errors = document.RootElement.GetProperty("errors");

        Assert.True(errors.TryGetProperty("Content", out _));
        Assert.True(errors.TryGetProperty("CustomerEmail", out _));
    }

    [Fact]
    public async Task Similar_ReturnsThresholdedCosineMatchesWithinCurrentWorkspace()
    {
        await using var factory = new PulsePilotApiFactory(database.ConnectionString);
        using var ownerClient = await CreateAuthenticatedClientAsync(factory, "similar-owner");
        using var outsiderClient = await CreateAuthenticatedClientAsync(factory, "similar-outsider");
        var source = await CreateFeedbackAsync(
            ownerClient,
            "Payment page freezes",
            FeedbackSource.Manual);
        var closeMatch = await CreateFeedbackAsync(
            ownerClient,
            "Checkout cannot complete",
            FeedbackSource.Api);
        var unrelated = await CreateFeedbackAsync(
            ownerClient,
            "Dark mode request",
            FeedbackSource.Manual);
        var pending = await CreateFeedbackAsync(
            ownerClient,
            "Pending feedback",
            FeedbackSource.Manual);

        await CompleteAnalysisAsync(factory, source.Id, CreateVector(1, 0));
        await CompleteAnalysisAsync(factory, closeMatch.Id, CreateVector(0.99f, 0.1f));
        await CompleteAnalysisAsync(factory, unrelated.Id, CreateVector(0, 1));

        var response = await ownerClient.GetFromJsonAsync<SimilarFeedbackResponse>(
            $"/api/feedback/{source.Id}/similar?limit=10",
            SerializerOptions);
        using var invalidLimitResponse = await ownerClient.GetAsync(
            $"/api/feedback/{source.Id}/similar?limit=0");
        using var pendingResponse = await ownerClient.GetAsync(
            $"/api/feedback/{pending.Id}/similar");
        using var outsiderResponse = await outsiderClient.GetAsync(
            $"/api/feedback/{source.Id}/similar");

        Assert.NotNull(response);
        Assert.Equal(source.Id, response.FeedbackId);
        Assert.Equal(SemanticSearchOptions.DefaultSimilarityThreshold, response.SimilarityThreshold);
        Assert.Equal(1, response.Count);
        var match = Assert.Single(response.Items);
        Assert.Equal(closeMatch.Id, match.Id);
        Assert.True(match.Similarity > 0.99);
        Assert.Equal(HttpStatusCode.BadRequest, invalidLimitResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, pendingResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, outsiderResponse.StatusCode);
    }

    private static async Task<HttpClient> CreateAuthenticatedClientAsync(
        PulsePilotApiFactory factory,
        string label)
    {
        var client = factory.CreateClient();
        var registerCommand = new RegisterCommand(
            $"{label}-{Guid.CreateVersion7():N}@example.com",
            $"{label} owner",
            "correct-horse-battery-staple",
            $"{label} workspace");
        using var response = await client.PostAsJsonAsync("/api/auth/register", registerCommand);
        response.EnsureSuccessStatusCode();
        var authentication = await response.Content.ReadFromJsonAsync<AuthenticationResponse>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            authentication!.AccessToken);

        return client;
    }

    private static async Task<FeedbackResponse> CreateFeedbackAsync(
        HttpClient client,
        string title,
        FeedbackSource source)
    {
        using var response = await PostAsJsonAsync(
            client,
            "/api/feedback",
            new CreateFeedbackCommand(title, $"{title} content", source, null, null));

        response.EnsureSuccessStatusCode();

        return (await ReadAsync<FeedbackResponse>(response))!;
    }

    private static async Task CompleteAnalysisAsync(
        PulsePilotApiFactory factory,
        Guid feedbackId,
        float[]? embeddingValues = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var feedback = await dbContext.Feedback.SingleAsync(entity => entity.Id == feedbackId);
        var processingLeaseId = feedback.StartProcessing(DateTimeOffset.UtcNow);
        var analyzedAt = DateTimeOffset.UtcNow;
        var analysis = FeedbackAnalysis.Create(
            feedback.WorkspaceId,
            feedback.Id,
            FeedbackCategory.Bug,
            FeedbackComponent.Payments,
            4,
            FeedbackSentiment.Negative,
            "User cannot add a payment card.",
            "Inspect payment tokenization failures.",
            0.94m,
            analyzedAt);
        var embeddingInput = FeedbackEmbeddingSource.CreateText(
            feedback.Title,
            feedback.Content);
        var embedding = FeedbackEmbedding.Create(
            feedback.WorkspaceId,
            feedback.Id,
            embeddingValues ?? CreateVector(1, 0),
            "integration-test-embedding-model",
            FeedbackEmbeddingSource.ComputeHash(embeddingInput),
            analyzedAt);

        dbContext.FeedbackAnalyses.Add(analysis);
        dbContext.FeedbackEmbeddings.Add(embedding);
        feedback.CompleteProcessing(processingLeaseId, analyzedAt);
        await dbContext.SaveChangesAsync();
    }

    private static float[] CreateVector(float first, float second)
    {
        var values = new float[FeedbackEmbedding.Dimensions];
        values[0] = first;
        values[1] = second;

        return values;
    }

    private static async Task MarkAnalysisFailedAsync(
        PulsePilotApiFactory factory,
        Guid feedbackId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var feedback = await dbContext.Feedback.SingleAsync(entity => entity.Id == feedbackId);
        var processingLeaseId = feedback.StartProcessing(DateTimeOffset.UtcNow);

        feedback.FailProcessing(processingLeaseId, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync();
    }

    private static Task<HttpResponseMessage> PostAsJsonAsync<T>(
        HttpClient client,
        string requestUri,
        T value)
    {
        return client.PostAsJsonAsync(requestUri, value, SerializerOptions);
    }

    private static Task<HttpResponseMessage> PutAsJsonAsync<T>(
        HttpClient client,
        string requestUri,
        T value)
    {
        return client.PutAsJsonAsync(requestUri, value, SerializerOptions);
    }

    private static Task<T?> ReadAsync<T>(HttpResponseMessage response)
    {
        return response.Content.ReadFromJsonAsync<T>(SerializerOptions);
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));

        return options;
    }
}
