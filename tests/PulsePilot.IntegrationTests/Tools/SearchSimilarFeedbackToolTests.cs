using Microsoft.Extensions.DependencyInjection;
using PulsePilot.Application;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Application.AI;
using PulsePilot.Application.Common.Exceptions;
using PulsePilot.Application.Tools;
using PulsePilot.Domain.Feedback;
using PulsePilot.Domain.Users;
using PulsePilot.Domain.Workspaces;
using PulsePilot.IntegrationTests.Persistence;

using FeedbackEntity = PulsePilot.Domain.Feedback.Feedback;

namespace PulsePilot.IntegrationTests.Tools;

public sealed class SearchSimilarFeedbackToolTests(PostgreSqlFixture database)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly IReadOnlyDictionary<string, string?> SearchConfiguration =
        new Dictionary<string, string?>
        {
            ["SemanticSearch:SimilarityThreshold"] = "0.90",
            ["SemanticSearch:DefaultLimit"] = "1",
            ["SemanticSearch:MaxLimit"] = "2",
        };

    [Fact]
    public async Task Tool_ReturnsConfiguredWorkspaceScopedMatchesInSimilarityOrder()
    {
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        var user = User.Create(
            $"similar-tool-{Guid.CreateVersion7():N}@example.com",
            "Similar Tool Owner",
            "password-hash",
            now);
        var workspace = Workspace.Create("Similar Tool Workspace", now);
        var otherWorkspace = Workspace.Create("Other Similar Tool Workspace", now);
        var source = CreateCompletedFeedback(
            workspace.Id,
            user.Id,
            "Payment page freezes",
            now);
        var closest = CreateCompletedFeedback(
            workspace.Id,
            user.Id,
            "Checkout cannot complete",
            now.AddSeconds(1));
        var second = CreateCompletedFeedback(
            workspace.Id,
            user.Id,
            "Card payment is rejected",
            now.AddSeconds(2));
        var unrelated = CreateCompletedFeedback(
            workspace.Id,
            user.Id,
            "Dark mode request",
            now.AddSeconds(3));
        var crossWorkspace = CreateCompletedFeedback(
            otherWorkspace.Id,
            user.Id,
            "Identical payment failure",
            now.AddSeconds(4));

        await using var serviceProvider = CreateServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();
        await SeedAsync(
            scope.ServiceProvider,
            user,
            [workspace, otherWorkspace],
            [source, closest, second, unrelated, crossWorkspace],
            [
                CreateEmbedding(source, CreateVector(1, 0), now.AddSeconds(10)),
                CreateEmbedding(closest, CreateVector(0.99f, 0.1f), now.AddSeconds(11)),
                CreateEmbedding(second, CreateVector(0.95f, 0.2f), now.AddSeconds(12)),
                CreateEmbedding(unrelated, CreateVector(0, 1), now.AddSeconds(13)),
                CreateEmbedding(crossWorkspace, CreateVector(1, 0), now.AddSeconds(14)),
            ]);
        var tool = scope.ServiceProvider.GetRequiredService<ISearchSimilarFeedbackTool>();

        var defaultResult = await tool.ExecuteAsync(
            workspace.Id,
            new SearchSimilarFeedbackToolInput(source.Id));
        var expandedResult = await tool.ExecuteAsync(
            workspace.Id,
            new SearchSimilarFeedbackToolInput(source.Id, Limit: 2));

        Assert.Equal(source.Id, defaultResult.FeedbackId);
        Assert.Equal(0.90, defaultResult.SimilarityThreshold);
        Assert.Equal(1, defaultResult.Count);
        Assert.Equal(closest.Id, Assert.Single(defaultResult.Items).FeedbackId);
        Assert.Equal([closest.Id, second.Id], expandedResult.Items.Select(item => item.FeedbackId));
        Assert.All(expandedResult.Items, item => Assert.True(item.Similarity >= 0.90));
        Assert.DoesNotContain(
            expandedResult.Items,
            item => item.FeedbackId == crossWorkspace.Id || item.FeedbackId == unrelated.Id);
    }

    [Fact]
    public async Task Tool_RejectsUnavailableCrossWorkspaceAndInvalidSearches()
    {
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        var user = User.Create(
            $"similar-tool-guards-{Guid.CreateVersion7():N}@example.com",
            "Similar Tool Guard Owner",
            "password-hash",
            now);
        var workspace = Workspace.Create("Similar Tool Guard Workspace", now);
        var otherWorkspace = Workspace.Create("Other Tool Guard Workspace", now);
        var pending = FeedbackEntity.Create(
            workspace.Id,
            user.Id,
            "Pending payment feedback",
            "Pending payment feedback content",
            FeedbackSource.Manual,
            null,
            null,
            now);
        var stale = CreateCompletedFeedback(
            workspace.Id,
            user.Id,
            "Stale payment feedback",
            now.AddSeconds(1));
        var crossWorkspace = CreateCompletedFeedback(
            otherWorkspace.Id,
            user.Id,
            "Other workspace payment feedback",
            now.AddSeconds(2));

        await using var serviceProvider = CreateServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();
        await SeedAsync(
            scope.ServiceProvider,
            user,
            [workspace, otherWorkspace],
            [pending, stale, crossWorkspace],
            [
                CreateEmbedding(
                    stale,
                    CreateVector(1, 0),
                    now.AddSeconds(10),
                    new string('f', FeedbackEmbedding.SourceHashLength)),
                CreateEmbedding(crossWorkspace, CreateVector(1, 0), now.AddSeconds(11)),
            ]);
        var tool = scope.ServiceProvider.GetRequiredService<ISearchSimilarFeedbackTool>();

        await Assert.ThrowsAsync<ConflictException>(() => tool.ExecuteAsync(
            workspace.Id,
            new SearchSimilarFeedbackToolInput(pending.Id)));
        await Assert.ThrowsAsync<ConflictException>(() => tool.ExecuteAsync(
            workspace.Id,
            new SearchSimilarFeedbackToolInput(stale.Id)));
        await Assert.ThrowsAsync<NotFoundException>(() => tool.ExecuteAsync(
            workspace.Id,
            new SearchSimilarFeedbackToolInput(crossWorkspace.Id)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => tool.ExecuteAsync(
            otherWorkspace.Id,
            new SearchSimilarFeedbackToolInput(crossWorkspace.Id, Limit: 3)));
        await Assert.ThrowsAsync<ArgumentException>(() => tool.ExecuteAsync(
            Guid.Empty,
            new SearchSimilarFeedbackToolInput(crossWorkspace.Id)));
        await Assert.ThrowsAsync<ArgumentException>(() => tool.ExecuteAsync(
            workspace.Id,
            new SearchSimilarFeedbackToolInput(Guid.Empty)));
    }

    private ServiceProvider CreateServiceProvider()
    {
        return database.CreateServiceProvider(
            SearchConfiguration,
            services => services.AddApplication());
    }

    private static async Task SeedAsync(
        IServiceProvider serviceProvider,
        User user,
        IReadOnlyList<Workspace> workspaces,
        IReadOnlyList<FeedbackEntity> feedback,
        IReadOnlyList<FeedbackEmbedding> embeddings)
    {
        await serviceProvider.GetRequiredService<IUserRepository>().AddAsync(user);
        var workspaceRepository = serviceProvider.GetRequiredService<IWorkspaceRepository>();
        var memberRepository = serviceProvider
            .GetRequiredService<IWorkspaceMemberRepository>();

        foreach (var workspace in workspaces)
        {
            await workspaceRepository.AddAsync(workspace);
            await memberRepository.AddAsync(WorkspaceMember.Join(
                workspace.Id,
                user.Id,
                WorkspaceRole.Admin,
                user.CreatedAt));
        }

        var feedbackRepository = serviceProvider.GetRequiredService<IFeedbackRepository>();

        foreach (var item in feedback)
        {
            await feedbackRepository.AddAsync(item);
        }

        var embeddingRepository = serviceProvider
            .GetRequiredService<IFeedbackEmbeddingRepository>();

        foreach (var embedding in embeddings)
        {
            await embeddingRepository.AddAsync(embedding);
        }

        await serviceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
    }

    private static FeedbackEntity CreateCompletedFeedback(
        Guid workspaceId,
        Guid userId,
        string title,
        DateTimeOffset createdAt)
    {
        var feedback = FeedbackEntity.Create(
            workspaceId,
            userId,
            title,
            $"{title} content",
            FeedbackSource.Manual,
            null,
            null,
            createdAt);
        var leaseId = feedback.StartProcessing(createdAt.AddMilliseconds(1));
        feedback.CompleteProcessing(leaseId, createdAt.AddMilliseconds(2));

        return feedback;
    }

    private static FeedbackEmbedding CreateEmbedding(
        FeedbackEntity feedback,
        float[] values,
        DateTimeOffset embeddedAt,
        string? sourceHash = null)
    {
        var embeddingSource = FeedbackEmbeddingSource.CreateText(
            feedback.Title,
            feedback.Content);

        return FeedbackEmbedding.Create(
            feedback.WorkspaceId,
            feedback.Id,
            values,
            "integration-test-model",
            sourceHash ?? FeedbackEmbeddingSource.ComputeHash(embeddingSource),
            embeddedAt);
    }

    private static float[] CreateVector(float first, float second)
    {
        var values = new float[FeedbackEmbedding.Dimensions];
        values[0] = first;
        values[1] = second;

        return values;
    }
}
