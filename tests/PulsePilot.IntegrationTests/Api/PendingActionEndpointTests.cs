using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Application.Actions;
using PulsePilot.Application.Authentication;
using PulsePilot.Domain.Actions;
using PulsePilot.Domain.Feedback;
using PulsePilot.IntegrationTests.Persistence;

using FeedbackEntity = PulsePilot.Domain.Feedback.Feedback;

namespace PulsePilot.IntegrationTests.Api;

public sealed class PendingActionEndpointTests(PostgreSqlFixture database)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    [Fact]
    public async Task ActionEndpoints_ReturnWorkspaceScopedPendingRecommendations()
    {
        await using var factory = new PulsePilotApiFactory(database.ConnectionString);
        var owner = await CreateAuthenticatedClientAsync(factory, "action-owner");
        var outsider = await CreateAuthenticatedClientAsync(factory, "action-outsider");
        using var ownerClient = owner.Client;
        using var outsiderClient = outsider.Client;
        using var anonymousClient = factory.CreateClient();
        var now = DateTimeOffset.UtcNow;
        var cluster = FeedbackCluster.Create(
            owner.Authentication.WorkspaceId,
            "Payment failures",
            FeedbackCategory.Bug,
            FeedbackComponent.Payments,
            now);
        cluster.UpdatePriority(82.5m, FeedbackPriority.P1, now);
        var feedback = FeedbackEntity.Create(
            owner.Authentication.WorkspaceId,
            owner.Authentication.UserId,
            "Card payment fails",
            "Every card payment has failed since the latest release.",
            FeedbackSource.Manual,
            null,
            null,
            now);
        feedback.AssignToCluster(cluster.Id, now);
        var pendingAction = PendingAction.Create(
            owner.Authentication.WorkspaceId,
            feedback.Id,
            cluster.Id,
            PendingActionType.CreateEngineeringIssue,
            "[P1] Payment failures",
            "Create an engineering issue for this cluster.",
            "{\"priority\":\"p1\",\"feedbackCount\":3}",
            now);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider
                .GetRequiredService<IFeedbackClusterRepository>()
                .AddAsync(cluster);
            await scope.ServiceProvider
                .GetRequiredService<IFeedbackRepository>()
                .AddAsync(feedback);
            await scope.ServiceProvider
                .GetRequiredService<IPendingActionRepository>()
                .AddAsync(pendingAction);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        }

        var list = await ownerClient.GetFromJsonAsync<PendingActionListResponse>(
            "/api/actions?page=1&pageSize=1&status=pending",
            SerializerOptions);
        var detail = await ownerClient.GetFromJsonAsync<PendingActionResponse>(
            $"/api/actions/{pendingAction.Id}",
            SerializerOptions);
        var outsiderList = await outsiderClient.GetFromJsonAsync<PendingActionListResponse>(
            "/api/actions",
            SerializerOptions);
        using var outsiderDetail = await outsiderClient.GetAsync(
            $"/api/actions/{pendingAction.Id}");
        using var anonymousResponse = await anonymousClient.GetAsync("/api/actions");
        using var invalidPageResponse = await ownerClient.GetAsync(
            "/api/actions?page=0&pageSize=20");
        using var invalidStatusResponse = await ownerClient.GetAsync(
            "/api/actions?status=not-a-status");

        Assert.NotNull(list);
        Assert.Equal(1, list.TotalCount);
        var summary = Assert.Single(list.Items);
        Assert.Equal(pendingAction.Id, summary.Id);
        Assert.Equal(PendingActionType.CreateEngineeringIssue, summary.ActionType);
        Assert.Equal(PendingActionStatus.Pending, summary.Status);

        Assert.NotNull(detail);
        Assert.Equal(pendingAction.Id, detail.Id);
        Assert.Equal(cluster.Id, detail.FeedbackClusterId);
        Assert.Equal("p1", detail.Payload.GetProperty("priority").GetString());
        Assert.Equal(3, detail.Payload.GetProperty("feedbackCount").GetInt32());
        Assert.Null(detail.ApprovedAt);
        Assert.Null(detail.ExecutedAt);

        Assert.NotNull(outsiderList);
        Assert.Equal(0, outsiderList.TotalCount);
        Assert.Empty(outsiderList.Items);
        Assert.Equal(HttpStatusCode.NotFound, outsiderDetail.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidPageResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidStatusResponse.StatusCode);
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
