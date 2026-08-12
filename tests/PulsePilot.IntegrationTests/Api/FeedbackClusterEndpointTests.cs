using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Application.Authentication;
using PulsePilot.Application.Feedback;
using PulsePilot.Application.FeedbackClusters;
using PulsePilot.Domain.Feedback;
using PulsePilot.IntegrationTests.Persistence;

using FeedbackEntity = PulsePilot.Domain.Feedback.Feedback;

namespace PulsePilot.IntegrationTests.Api;

public sealed class FeedbackClusterEndpointTests(PostgreSqlFixture database)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    [Fact]
    public async Task ClusterEndpoints_ReturnPaginatedWorkspaceScopedSummariesAndMembers()
    {
        await using var factory = new PulsePilotApiFactory(database.ConnectionString);
        var owner = await CreateAuthenticatedClientAsync(factory, "cluster-owner");
        var outsider = await CreateAuthenticatedClientAsync(factory, "cluster-outsider");
        using var ownerClient = owner.Client;
        using var outsiderClient = outsider.Client;
        using var anonymousClient = factory.CreateClient();
        var cluster = FeedbackCluster.Create(
            owner.Authentication.WorkspaceId,
            "Payment failures",
            FeedbackCategory.Bug,
            FeedbackComponent.Payments,
            DateTimeOffset.UtcNow);
        var firstFeedback = CreateFeedback(
            owner.Authentication,
            "Card payment fails",
            DateTimeOffset.UtcNow);
        var secondFeedback = CreateFeedback(
            owner.Authentication,
            "Checkout is frozen",
            DateTimeOffset.UtcNow.AddSeconds(1));
        firstFeedback.AssignToCluster(cluster.Id, DateTimeOffset.UtcNow.AddMinutes(1));
        secondFeedback.AssignToCluster(cluster.Id, DateTimeOffset.UtcNow.AddMinutes(1));
        cluster.UpdatePriority(82.5m, FeedbackPriority.P1, DateTimeOffset.UtcNow.AddMinutes(1));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider
                .GetRequiredService<IFeedbackClusterRepository>()
                .AddAsync(cluster);
            var feedbackRepository = scope.ServiceProvider.GetRequiredService<IFeedbackRepository>();
            await feedbackRepository.AddAsync(firstFeedback);
            await feedbackRepository.AddAsync(secondFeedback);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        }

        var list = await ownerClient.GetFromJsonAsync<FeedbackClusterListResponse>(
            "/api/clusters?page=1&pageSize=1",
            SerializerOptions);
        var detail = await ownerClient.GetFromJsonAsync<FeedbackClusterDetailResponse>(
            $"/api/clusters/{cluster.Id}?page=1&pageSize=1",
            SerializerOptions);
        var outsiderList = await outsiderClient.GetFromJsonAsync<FeedbackClusterListResponse>(
            "/api/clusters",
            SerializerOptions);
        using var outsiderDetail = await outsiderClient.GetAsync($"/api/clusters/{cluster.Id}");
        using var anonymousResponse = await anonymousClient.GetAsync("/api/clusters");
        using var invalidPageResponse = await ownerClient.GetAsync(
            "/api/clusters?page=0&pageSize=20");

        Assert.NotNull(list);
        Assert.Equal(1, list.TotalCount);
        var summary = Assert.Single(list.Items);
        Assert.Equal(cluster.Id, summary.Id);
        Assert.Equal(2, summary.FeedbackCount);
        Assert.Equal(FeedbackCategory.Bug, summary.Category);
        Assert.Equal(FeedbackComponent.Payments, summary.Component);
        Assert.Equal(82.5m, summary.PriorityScore);
        Assert.Equal(FeedbackPriority.P1, summary.Priority);

        Assert.NotNull(detail);
        Assert.Equal(cluster.Id, detail.Id);
        Assert.Equal(2, detail.TotalFeedbackCount);
        Assert.Equal(82.5m, detail.PriorityScore);
        Assert.Equal(FeedbackPriority.P1, detail.Priority);
        Assert.Single(detail.Feedback);
        Assert.Equal(1, detail.Page);
        Assert.Equal(1, detail.PageSize);

        Assert.NotNull(outsiderList);
        Assert.Equal(0, outsiderList.TotalCount);
        Assert.Empty(outsiderList.Items);
        Assert.Equal(HttpStatusCode.NotFound, outsiderDetail.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidPageResponse.StatusCode);
    }

    [Fact]
    public async Task FeedbackMutation_RecalculatesPreviousClusterPriority()
    {
        await using var factory = new PulsePilotApiFactory(database.ConnectionString);
        var owner = await CreateAuthenticatedClientAsync(factory, "priority-mutation-owner");
        using var client = owner.Client;
        var now = DateTimeOffset.UtcNow;
        var cluster = FeedbackCluster.Create(
            owner.Authentication.WorkspaceId,
            "Payment failures",
            FeedbackCategory.Bug,
            FeedbackComponent.Payments,
            now);
        var criticalFeedback = CreateFeedback(
            owner.Authentication,
            "Critical card failure",
            now);
        var minorFeedback = CreateFeedback(
            owner.Authentication,
            "Minor card warning",
            now);
        criticalFeedback.AssignToCluster(cluster.Id, now);
        minorFeedback.AssignToCluster(cluster.Id, now);
        cluster.UpdatePriority(100m, FeedbackPriority.P1, now);
        var criticalAnalysis = CreateAnalysis(criticalFeedback, severity: 5, now);
        var minorAnalysis = CreateAnalysis(minorFeedback, severity: 1, now);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider
                .GetRequiredService<IFeedbackClusterRepository>()
                .AddAsync(cluster);
            var feedbackRepository = scope.ServiceProvider.GetRequiredService<IFeedbackRepository>();
            await feedbackRepository.AddAsync(criticalFeedback);
            await feedbackRepository.AddAsync(minorFeedback);
            var analysisRepository = scope.ServiceProvider
                .GetRequiredService<IFeedbackAnalysisRepository>();
            await analysisRepository.AddAsync(criticalAnalysis);
            await analysisRepository.AddAsync(minorAnalysis);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        }

        using var updateResponse = await client.PutAsJsonAsync(
            $"/api/feedback/{criticalFeedback.Id}",
            new UpdateFeedbackCommand(
                "Updated critical report",
                "Updated critical report content",
                FeedbackSource.Manual,
                null,
                null),
            SerializerOptions);
        updateResponse.EnsureSuccessStatusCode();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var updatedCluster = await scope.ServiceProvider
                .GetRequiredService<IFeedbackClusterRepository>()
                .GetByIdAsync(owner.Authentication.WorkspaceId, cluster.Id);

            Assert.NotNull(updatedCluster);
            Assert.Equal(25.50m, updatedCluster.PriorityScore);
            Assert.Equal(FeedbackPriority.P3, updatedCluster.Priority);
        }

        using var deleteResponse = await client.DeleteAsync($"/api/feedback/{minorFeedback.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var emptyCluster = await scope.ServiceProvider
                .GetRequiredService<IFeedbackClusterRepository>()
                .GetByIdAsync(owner.Authentication.WorkspaceId, cluster.Id);

            Assert.NotNull(emptyCluster);
            Assert.Equal(0m, emptyCluster.PriorityScore);
            Assert.Equal(FeedbackPriority.P4, emptyCluster.Priority);
        }
    }

    private static async Task<AuthenticatedClient> CreateAuthenticatedClientAsync(
        PulsePilotApiFactory factory,
        string label)
    {
        var client = factory.CreateClient();
        var registerCommand = new RegisterCommand(
            $"{label}-{Guid.CreateVersion7():N}@example.com",
            $"{label} owner",
            "correct-horse-battery-staple",
            $"{label} workspace");
        using var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            registerCommand,
            SerializerOptions);
        response.EnsureSuccessStatusCode();
        var authentication = await response.Content.ReadFromJsonAsync<AuthenticationResponse>(
            SerializerOptions);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            authentication!.AccessToken);

        return new AuthenticatedClient(client, authentication);
    }

    private static FeedbackEntity CreateFeedback(
        AuthenticationResponse authentication,
        string title,
        DateTimeOffset createdAt)
    {
        return FeedbackEntity.Create(
            authentication.WorkspaceId,
            authentication.UserId,
            title,
            $"{title} content",
            FeedbackSource.Manual,
            null,
            null,
            createdAt);
    }

    private static FeedbackAnalysis CreateAnalysis(
        FeedbackEntity feedback,
        int severity,
        DateTimeOffset analyzedAt)
    {
        return FeedbackAnalysis.Create(
            feedback.WorkspaceId,
            feedback.Id,
            FeedbackCategory.Bug,
            FeedbackComponent.Payments,
            severity,
            FeedbackSentiment.Negative,
            "Payment feedback summary.",
            "Investigate the payment flow.",
            0.95m,
            analyzedAt);
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));

        return options;
    }

    private sealed record AuthenticatedClient(
        HttpClient Client,
        AuthenticationResponse Authentication);
}
