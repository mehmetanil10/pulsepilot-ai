using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Application.Actions;
using PulsePilot.Application.Authentication;
using PulsePilot.Application.Backlog;
using PulsePilot.Domain.Actions;
using PulsePilot.Domain.Backlog;
using PulsePilot.Domain.Feedback;
using PulsePilot.IntegrationTests.Persistence;

using FeedbackEntity = PulsePilot.Domain.Feedback.Feedback;

namespace PulsePilot.IntegrationTests.Api;

public sealed class BacklogEndpointTests(PostgreSqlFixture database)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    [Fact]
    public async Task BacklogEndpoints_ReturnWorkspaceScopedToolResults()
    {
        await using var factory = new PulsePilotApiFactory(database.ConnectionString);
        var owner = await CreateAuthenticatedClientAsync(factory, "backlog-owner");
        var outsider = await CreateAuthenticatedClientAsync(factory, "backlog-outsider");
        using var ownerClient = owner.Client;
        using var outsiderClient = outsider.Client;
        using var anonymousClient = factory.CreateClient();
        var pendingAction = await SeedPendingActionAsync(factory, owner.Authentication);

        using var approveResponse = await ownerClient.PostAsync(
            $"/api/actions/{pendingAction.Id}/approve",
            content: null);
        var executedAction = await approveResponse.Content
            .ReadFromJsonAsync<PendingActionResponse>(SerializerOptions);
        var list = await ownerClient.GetFromJsonAsync<BacklogItemListResponse>(
            $"/api/backlog?page=1&pageSize=1&status=open&priority=p2"
                + $"&sourcePendingActionId={pendingAction.Id}",
            SerializerOptions);
        var backlogItem = Assert.Single(list!.Items);
        var detail = await ownerClient.GetFromJsonAsync<BacklogItemResponse>(
            $"/api/backlog/{backlogItem.Id}",
            SerializerOptions);
        var outsiderList = await outsiderClient.GetFromJsonAsync<BacklogItemListResponse>(
            "/api/backlog",
            SerializerOptions);
        using var outsiderDetail = await outsiderClient.GetAsync(
            $"/api/backlog/{backlogItem.Id}");
        using var anonymousResponse = await anonymousClient.GetAsync("/api/backlog");
        using var invalidPageResponse = await ownerClient.GetAsync(
            "/api/backlog?page=0&pageSize=20");
        using var invalidStatusResponse = await ownerClient.GetAsync(
            "/api/backlog?status=not-a-status");
        using var emptySourceActionResponse = await ownerClient.GetAsync(
            "/api/backlog?sourcePendingActionId=00000000-0000-0000-0000-000000000000");

        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        Assert.Equal(PendingActionStatus.Executed, executedAction?.Status);
        Assert.NotNull(executedAction?.ExecutedAt);

        Assert.Equal(1, list.TotalCount);
        Assert.Equal(1, list.Page);
        Assert.Equal(1, list.PageSize);
        Assert.Equal(pendingAction.Id, backlogItem.SourcePendingActionId);
        Assert.Equal(pendingAction.FeedbackClusterId, backlogItem.SourceClusterId);
        Assert.Equal(owner.Authentication.UserId, backlogItem.CreatedByUserId);
        Assert.Equal(BacklogItemPriority.P2, backlogItem.Priority);
        Assert.Equal(BacklogItemStatus.Open, backlogItem.Status);
        Assert.Equal(pendingAction.Title, backlogItem.Title);

        Assert.NotNull(detail);
        Assert.Equal(backlogItem, detail);
        Assert.NotNull(outsiderList);
        Assert.Equal(0, outsiderList.TotalCount);
        Assert.Empty(outsiderList.Items);
        Assert.Equal(HttpStatusCode.NotFound, outsiderDetail.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidPageResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidStatusResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, emptySourceActionResponse.StatusCode);
    }

    private static async Task<AuthenticatedClient> CreateAuthenticatedClientAsync(
        PulsePilotApiFactory factory,
        string label)
    {
        var client = factory.CreateClient();
        var command = new RegisterCommand(
            $"{label}-{Guid.CreateVersion7():N}@example.com",
            $"{label} owner",
            "correct-horse-battery-staple",
            $"{label} workspace");
        using var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            command,
            SerializerOptions);
        response.EnsureSuccessStatusCode();
        var authentication = await response.Content.ReadFromJsonAsync<AuthenticationResponse>(
            SerializerOptions);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            authentication!.AccessToken);

        return new AuthenticatedClient(client, authentication);
    }

    private static async Task<PendingAction> SeedPendingActionAsync(
        PulsePilotApiFactory factory,
        AuthenticationResponse owner)
    {
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        var cluster = FeedbackCluster.Create(
            owner.WorkspaceId,
            "Checkout payment failures",
            FeedbackCategory.Bug,
            FeedbackComponent.Payments,
            now);
        cluster.UpdatePriority(62.5m, FeedbackPriority.P2, now.AddSeconds(1));
        var feedback = FeedbackEntity.Create(
            owner.WorkspaceId,
            owner.UserId,
            "Checkout fails",
            "Checkout rejects every payment card.",
            FeedbackSource.Manual,
            null,
            null,
            now);
        feedback.AssignToCluster(cluster.Id, now.AddSeconds(1));
        var pendingAction = PendingAction.Create(
            owner.WorkspaceId,
            feedback.Id,
            cluster.Id,
            PendingActionType.CreateEngineeringIssue,
            "[P2] Checkout payment failures",
            "Create an engineering issue for repeated checkout failures.",
            "{\"priority\":\"p2\"}",
            now.AddSeconds(1));

        await using var scope = factory.Services.CreateAsyncScope();
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

        return pendingAction;
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
