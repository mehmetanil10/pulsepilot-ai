using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PulsePilot.Application.Abstractions.Authentication;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Application.Actions;
using PulsePilot.Application.Authentication;
using PulsePilot.Domain.Actions;
using PulsePilot.Domain.Feedback;
using PulsePilot.Domain.Users;
using PulsePilot.Domain.Workspaces;
using PulsePilot.Infrastructure.Persistence;
using PulsePilot.IntegrationTests.Persistence;

using FeedbackEntity = PulsePilot.Domain.Feedback.Feedback;

namespace PulsePilot.IntegrationTests.Api;

public sealed class PendingActionReviewEndpointTests(PostgreSqlFixture database)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    [Fact]
    public async Task ReviewEndpoints_EnforceAdminWorkspaceAndIdempotentDecisions()
    {
        await using var factory = new PulsePilotApiFactory(database.ConnectionString);
        var owner = await CreateAuthenticatedClientAsync(factory, "review-owner");
        var outsider = await CreateAuthenticatedClientAsync(factory, "review-outsider");
        using var ownerClient = owner.Client;
        using var outsiderClient = outsider.Client;
        using var memberClient = await CreateMemberClientAsync(factory, owner.Authentication);
        using var anonymousClient = factory.CreateClient();
        var actions = await SeedPendingActionsAsync(factory, owner.Authentication, count: 3);

        using var approveResponse = await ownerClient.PostAsync(
            $"/api/actions/{actions[0].Id}/approve",
            content: null);
        var approved = await approveResponse.Content.ReadFromJsonAsync<PendingActionResponse>(
            SerializerOptions);
        using var repeatedApproveResponse = await ownerClient.PostAsync(
            $"/api/actions/{actions[0].Id}/approve",
            content: null);
        var repeatedApproval = await repeatedApproveResponse.Content
            .ReadFromJsonAsync<PendingActionResponse>(SerializerOptions);
        using var conflictingRejectResponse = await ownerClient.PostAsync(
            $"/api/actions/{actions[0].Id}/reject",
            content: null);

        using var rejectResponse = await ownerClient.PostAsync(
            $"/api/actions/{actions[1].Id}/reject",
            content: null);
        var rejected = await rejectResponse.Content.ReadFromJsonAsync<PendingActionResponse>(
            SerializerOptions);
        using var repeatedRejectResponse = await ownerClient.PostAsync(
            $"/api/actions/{actions[1].Id}/reject",
            content: null);
        var repeatedRejection = await repeatedRejectResponse.Content
            .ReadFromJsonAsync<PendingActionResponse>(SerializerOptions);

        using var memberResponse = await memberClient.PostAsync(
            $"/api/actions/{actions[2].Id}/approve",
            content: null);
        using var outsiderResponse = await outsiderClient.PostAsync(
            $"/api/actions/{actions[2].Id}/approve",
            content: null);
        using var anonymousResponse = await anonymousClient.PostAsync(
            $"/api/actions/{actions[2].Id}/approve",
            content: null);

        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        Assert.NotNull(approved);
        Assert.Equal(PendingActionStatus.Approved, approved.Status);
        Assert.NotNull(approved.ApprovedAt);
        Assert.Null(approved.RejectedAt);
        Assert.Equal(HttpStatusCode.OK, repeatedApproveResponse.StatusCode);
        Assert.Equal(approved.ApprovedAt, repeatedApproval?.ApprovedAt);
        Assert.Equal(HttpStatusCode.Conflict, conflictingRejectResponse.StatusCode);

        Assert.Equal(HttpStatusCode.OK, rejectResponse.StatusCode);
        Assert.NotNull(rejected);
        Assert.Equal(PendingActionStatus.Rejected, rejected.Status);
        Assert.NotNull(rejected.RejectedAt);
        Assert.Null(rejected.ApprovedAt);
        Assert.Equal(HttpStatusCode.OK, repeatedRejectResponse.StatusCode);
        Assert.Equal(rejected.RejectedAt, repeatedRejection?.RejectedAt);

        Assert.Equal(HttpStatusCode.Forbidden, memberResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, outsiderResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var pendingAction = await verificationScope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .PendingActions
            .AsNoTracking()
            .SingleAsync(action => action.Id == actions[2].Id);
        Assert.Equal(PendingActionStatus.Pending, pendingAction.Status);
    }

    [Fact]
    public async Task ConcurrentOppositeDecisions_AllowExactlyOneReview()
    {
        await using var factory = new PulsePilotApiFactory(database.ConnectionString);
        var owner = await CreateAuthenticatedClientAsync(factory, "review-concurrency");
        using var ownerClient = owner.Client;
        var action = Assert.Single(
            await SeedPendingActionsAsync(factory, owner.Authentication, count: 1));

        var responses = await Task.WhenAll(
            ownerClient.PostAsync($"/api/actions/{action.Id}/approve", content: null),
            ownerClient.PostAsync($"/api/actions/{action.Id}/reject", content: null));

        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);

            await using var scope = factory.Services.CreateAsyncScope();
            var persistedAction = await scope.ServiceProvider
                .GetRequiredService<AppDbContext>()
                .PendingActions
                .AsNoTracking()
                .SingleAsync(candidate => candidate.Id == action.Id);

            Assert.True(
                persistedAction.Status is PendingActionStatus.Approved
                    or PendingActionStatus.Rejected);
            Assert.NotEqual(
                persistedAction.ApprovedAt.HasValue,
                persistedAction.RejectedAt.HasValue);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
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

    private static async Task<HttpClient> CreateMemberClientAsync(
        PulsePilotApiFactory factory,
        AuthenticationResponse owner)
    {
        var now = DateTimeOffset.UtcNow;
        var member = User.Create(
            $"review-member-{Guid.CreateVersion7():N}@example.com",
            "Review Member",
            "password-hash",
            now);
        var membership = WorkspaceMember.Join(
            owner.WorkspaceId,
            member.Id,
            WorkspaceRole.Member,
            now);

        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IUserRepository>().AddAsync(member);
        await scope.ServiceProvider
            .GetRequiredService<IWorkspaceMemberRepository>()
            .AddAsync(membership);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        var token = scope.ServiceProvider
            .GetRequiredService<IAccessTokenGenerator>()
            .Generate(member, membership, now);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token.Value);

        return client;
    }

    private static async Task<IReadOnlyList<PendingAction>> SeedPendingActionsAsync(
        PulsePilotApiFactory factory,
        AuthenticationResponse owner,
        int count)
    {
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        var actions = new List<PendingAction>(count);

        await using var scope = factory.Services.CreateAsyncScope();
        var clusterRepository = scope.ServiceProvider
            .GetRequiredService<IFeedbackClusterRepository>();
        var feedbackRepository = scope.ServiceProvider.GetRequiredService<IFeedbackRepository>();
        var actionRepository = scope.ServiceProvider
            .GetRequiredService<IPendingActionRepository>();

        for (var index = 0; index < count; index++)
        {
            var cluster = FeedbackCluster.Create(
                owner.WorkspaceId,
                $"Payment failures {index}",
                FeedbackCategory.Bug,
                FeedbackComponent.Payments,
                now.AddMilliseconds(index));
            var feedback = FeedbackEntity.Create(
                owner.WorkspaceId,
                owner.UserId,
                $"Card payment fails {index}",
                "Every card payment has failed since the latest release.",
                FeedbackSource.Manual,
                null,
                null,
                now.AddMilliseconds(index));
            feedback.AssignToCluster(cluster.Id, now.AddSeconds(1));
            var action = PendingAction.Create(
                owner.WorkspaceId,
                feedback.Id,
                cluster.Id,
                PendingActionType.CreateEngineeringIssue,
                $"[P1] Payment failures {index}",
                "Create an engineering issue for this cluster.",
                "{\"priority\":\"p1\"}",
                now.AddSeconds(1));

            await clusterRepository.AddAsync(cluster);
            await feedbackRepository.AddAsync(feedback);
            await actionRepository.AddAsync(action);
            actions.Add(action);
        }

        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

        return actions;
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
