using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Application.Authentication;
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

        Assert.NotNull(detail);
        Assert.Equal(cluster.Id, detail.Id);
        Assert.Equal(2, detail.TotalFeedbackCount);
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
