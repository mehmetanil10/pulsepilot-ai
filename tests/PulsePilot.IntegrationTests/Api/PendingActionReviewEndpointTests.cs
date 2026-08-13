using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PulsePilot.Application.Abstractions.AI;
using PulsePilot.Application.Abstractions.Authentication;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Application.Actions;
using PulsePilot.Application.AI;
using PulsePilot.Application.Authentication;
using PulsePilot.Application.Common.Exceptions;
using PulsePilot.Application.CustomerResponses;
using PulsePilot.Domain.Actions;
using PulsePilot.Domain.Backlog;
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
        var llmClient = new CustomerResponseLlmClient();
        await using var factory = new PulsePilotApiFactory(
            database.ConnectionString,
            services =>
            {
                services.RemoveAll<ILLMClient>();
                services.AddSingleton<ILLMClient>(llmClient);
            });
        var owner = await CreateAuthenticatedClientAsync(factory, "review-owner");
        var outsider = await CreateAuthenticatedClientAsync(factory, "review-outsider");
        using var ownerClient = owner.Client;
        using var outsiderClient = outsider.Client;
        using var memberClient = await CreateMemberClientAsync(factory, owner.Authentication);
        using var anonymousClient = factory.CreateClient();
        var actions = await SeedPendingActionsAsync(factory, owner.Authentication, count: 3);
        var customerResponseAction = Assert.Single(await SeedPendingActionsAsync(
            factory,
            owner.Authentication,
            count: 1,
            actionType: PendingActionType.DraftCustomerResponse));

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
        using var customerResponseApproveResponse = await ownerClient.PostAsync(
            $"/api/actions/{customerResponseAction.Id}/approve",
            content: null);
        var approvedCustomerResponse = await customerResponseApproveResponse.Content
            .ReadFromJsonAsync<PendingActionResponse>(SerializerOptions);
        using var repeatedCustomerResponseApprove = await ownerClient.PostAsync(
            $"/api/actions/{customerResponseAction.Id}/approve",
            content: null);
        using var customerDraftResponse = await ownerClient.GetAsync(
            $"/api/actions/{customerResponseAction.Id}/customer-response-draft");
        var customerDraft = await customerDraftResponse.Content
            .ReadFromJsonAsync<CustomerResponseDraftResponse>(SerializerOptions);
        using var outsiderDraftResponse = await outsiderClient.GetAsync(
            $"/api/actions/{customerResponseAction.Id}/customer-response-draft");
        using var anonymousDraftResponse = await anonymousClient.GetAsync(
            $"/api/actions/{customerResponseAction.Id}/customer-response-draft");

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
        Assert.Equal(PendingActionStatus.Executed, approved.Status);
        Assert.NotNull(approved.ApprovedAt);
        Assert.NotNull(approved.ExecutedAt);
        Assert.Null(approved.RejectedAt);
        Assert.Equal(HttpStatusCode.OK, repeatedApproveResponse.StatusCode);
        Assert.Equal(approved.ApprovedAt, repeatedApproval?.ApprovedAt);
        Assert.Equal(approved.ExecutedAt, repeatedApproval?.ExecutedAt);
        Assert.Equal(HttpStatusCode.Conflict, conflictingRejectResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, customerResponseApproveResponse.StatusCode);
        Assert.Equal(PendingActionStatus.Executed, approvedCustomerResponse?.Status);
        Assert.NotNull(approvedCustomerResponse?.ExecutedAt);
        Assert.Equal(HttpStatusCode.OK, repeatedCustomerResponseApprove.StatusCode);
        Assert.Equal(1, llmClient.DraftCallCount);
        Assert.Equal(HttpStatusCode.OK, customerDraftResponse.StatusCode);
        Assert.NotNull(customerDraft);
        Assert.Equal(customerResponseAction.Id, customerDraft.SourcePendingActionId);
        Assert.Equal(owner.Authentication.UserId, customerDraft.CreatedByUserId);
        Assert.Equal(CustomerResponseLlmClient.DraftContent, customerDraft.Content);
        Assert.Equal(HttpStatusCode.NotFound, outsiderDraftResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousDraftResponse.StatusCode);

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
        var backlogItem = await verificationScope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .BacklogItems
            .AsNoTracking()
            .SingleAsync(item => item.SourcePendingActionId == actions[0].Id);
        Assert.Equal(BacklogItemPriority.P1, backlogItem.Priority);
        Assert.Equal(BacklogItemStatus.Open, backlogItem.Status);
        Assert.Equal(owner.Authentication.UserId, backlogItem.CreatedByUserId);
        Assert.False(await verificationScope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .BacklogItems
            .AnyAsync(item =>
                item.SourcePendingActionId == customerResponseAction.Id));
        var sourceFeedback = await verificationScope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Feedback
            .SingleAsync(feedback => feedback.Id == actions[0].FeedbackId);
        var sourceCluster = await verificationScope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .FeedbackClusters
            .SingleAsync(cluster => cluster.Id == actions[0].FeedbackClusterId);
        var duplicateRecommendation = await verificationScope.ServiceProvider
            .GetRequiredService<IPendingActionRecommender>()
            .RecommendAsync(new ActionRecommendationContext(
                sourceFeedback,
                sourceCluster,
                new FeedbackAnalysisResult(
                    FeedbackCategory.Bug,
                    FeedbackComponent.Payments,
                    5,
                    FeedbackSentiment.Negative,
                    "Payment failures remain critical.",
                    "Create another engineering issue.",
                    0.98m),
                FeedbackCount: 10,
                RecommendedAt: DateTimeOffset.UtcNow));
        Assert.Null(duplicateRecommendation);
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
                persistedAction.Status is PendingActionStatus.Executed
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

    [Fact]
    public async Task CustomerResponseApproval_WhenProviderFails_LeavesActionPendingWithoutDraft()
    {
        var llmClient = new FailingCustomerResponseLlmClient();
        await using var factory = new PulsePilotApiFactory(
            database.ConnectionString,
            services =>
            {
                services.RemoveAll<ILLMClient>();
                services.AddSingleton<ILLMClient>(llmClient);
            });
        var owner = await CreateAuthenticatedClientAsync(factory, "draft-provider-failure");
        using var ownerClient = owner.Client;
        var action = Assert.Single(await SeedPendingActionsAsync(
            factory,
            owner.Authentication,
            count: 1,
            actionType: PendingActionType.DraftCustomerResponse));

        using var response = await ownerClient.PostAsync(
            $"/api/actions/{action.Id}/approve",
            content: null);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(2, llmClient.DraftCallCount);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persistedAction = await dbContext.PendingActions
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == action.Id);

        Assert.Equal(PendingActionStatus.Pending, persistedAction.Status);
        Assert.False(await dbContext.CustomerResponseDrafts
            .AsNoTracking()
            .AnyAsync(draft => draft.SourcePendingActionId == action.Id));
    }

    [Fact]
    public async Task ConcurrentIdenticalApprovals_CreateExactlyOneBacklogItem()
    {
        await using var factory = new PulsePilotApiFactory(database.ConnectionString);
        var owner = await CreateAuthenticatedClientAsync(factory, "review-idempotency");
        using var ownerClient = owner.Client;
        var action = Assert.Single(
            await SeedPendingActionsAsync(factory, owner.Authentication, count: 1));

        var responses = await Task.WhenAll(
            ownerClient.PostAsync($"/api/actions/{action.Id}/approve", content: null),
            ownerClient.PostAsync($"/api/actions/{action.Id}/approve", content: null));

        try
        {
            Assert.All(
                responses,
                response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));

            await using var scope = factory.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var persistedAction = await dbContext.PendingActions
                .AsNoTracking()
                .SingleAsync(candidate => candidate.Id == action.Id);
            var backlogItems = await dbContext.BacklogItems
                .AsNoTracking()
                .Where(item => item.SourcePendingActionId == action.Id)
                .ToListAsync();

            Assert.Equal(PendingActionStatus.Executed, persistedAction.Status);
            Assert.Single(backlogItems);
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
        int count,
        PendingActionType actionType = PendingActionType.CreateEngineeringIssue)
    {
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        var actions = new List<PendingAction>(count);

        await using var scope = factory.Services.CreateAsyncScope();
        var clusterRepository = scope.ServiceProvider
            .GetRequiredService<IFeedbackClusterRepository>();
        var feedbackRepository = scope.ServiceProvider.GetRequiredService<IFeedbackRepository>();
        var actionRepository = scope.ServiceProvider
            .GetRequiredService<IPendingActionRepository>();
        var analysisRepository = scope.ServiceProvider
            .GetRequiredService<IFeedbackAnalysisRepository>();

        for (var index = 0; index < count; index++)
        {
            var cluster = FeedbackCluster.Create(
                owner.WorkspaceId,
                $"Payment failures {index}",
                FeedbackCategory.Bug,
                FeedbackComponent.Payments,
                now.AddMilliseconds(index));
            cluster.UpdatePriority(
                85m,
                FeedbackPriority.P1,
                now.AddSeconds(1));
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

            if (actionType == PendingActionType.DraftCustomerResponse)
            {
                var leaseId = feedback.StartProcessing(now.AddSeconds(2));
                feedback.CompleteProcessing(leaseId, now.AddSeconds(3));
                await analysisRepository.AddAsync(FeedbackAnalysis.Create(
                    owner.WorkspaceId,
                    feedback.Id,
                    FeedbackCategory.Complaint,
                    FeedbackComponent.Payments,
                    4,
                    FeedbackSentiment.Negative,
                    "The customer cannot complete a card payment.",
                    "Draft an empathetic response for human review.",
                    0.95m,
                    now.AddSeconds(3)));
            }

            var action = PendingAction.Create(
                owner.WorkspaceId,
                feedback.Id,
                cluster.Id,
                actionType,
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

    private sealed class CustomerResponseLlmClient : ILLMClient
    {
        public const string DraftContent =
            "We're sorry you're experiencing this payment issue. Our team is reviewing the report, and we appreciate you bringing it to our attention.";

        private int _draftCallCount;

        public int DraftCallCount => _draftCallCount;

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
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _draftCallCount);

            return Task.FromResult(new CustomerResponseDraftResult(DraftContent));
        }
    }

    private sealed class FailingCustomerResponseLlmClient : ILLMClient
    {
        private int _draftCallCount;

        public int DraftCallCount => _draftCallCount;

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
            Interlocked.Increment(ref _draftCallCount);

            throw new LlmProviderException(
                LlmProviderFailureKind.ProviderUnavailable,
                "The AI provider is temporarily unavailable.",
                isTransient: true);
        }
    }
}
