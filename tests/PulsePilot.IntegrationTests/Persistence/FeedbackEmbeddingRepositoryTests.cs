using Microsoft.Extensions.DependencyInjection;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Domain.Feedback;
using PulsePilot.Domain.Users;
using PulsePilot.Domain.Workspaces;

using FeedbackEntity = PulsePilot.Domain.Feedback.Feedback;

namespace PulsePilot.IntegrationTests.Persistence;

public sealed class FeedbackEmbeddingRepositoryTests(PostgreSqlFixture database)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Repository_PersistsVectorAndReturnsWorkspaceScopedCosineMatches()
    {
        var now = DateTimeOffset.UtcNow;
        var user = User.Create(
            $"embedding-{Guid.CreateVersion7():N}@example.com",
            "Embedding Owner",
            "password-hash",
            now);
        var workspace = Workspace.Create("Embedding Workspace", now);
        var otherWorkspace = Workspace.Create("Other Embedding Workspace", now);
        var membership = WorkspaceMember.Join(
            workspace.Id,
            user.Id,
            WorkspaceRole.Admin,
            now);
        var source = CreateCompletedFeedback(
            workspace.Id,
            user.Id,
            "Payment page freezes",
            now);
        var closeMatch = CreateCompletedFeedback(
            workspace.Id,
            user.Id,
            "Checkout cannot complete",
            now.AddSeconds(1));
        var belowThreshold = CreateCompletedFeedback(
            workspace.Id,
            user.Id,
            "Dark mode request",
            now.AddSeconds(2));
        var crossWorkspaceMatch = CreateCompletedFeedback(
            otherWorkspace.Id,
            user.Id,
            "Another payment failure",
            now.AddSeconds(3));
        var sourceEmbedding = CreateEmbedding(source, CreateVector(1, 0), 'a', now.AddMinutes(1));
        var closeEmbedding = CreateEmbedding(closeMatch, CreateVector(0.99f, 0.1f), 'b', now.AddMinutes(1));
        var belowThresholdEmbedding = CreateEmbedding(
            belowThreshold,
            CreateVector(0, 1),
            'c',
            now.AddMinutes(1));
        var crossWorkspaceEmbedding = CreateEmbedding(
            crossWorkspaceMatch,
            CreateVector(1, 0),
            'd',
            now.AddMinutes(1));

        await using (var serviceProvider = database.CreateServiceProvider())
        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IUserRepository>().AddAsync(user);
            await scope.ServiceProvider.GetRequiredService<IWorkspaceRepository>().AddAsync(workspace);
            await scope.ServiceProvider
                .GetRequiredService<IWorkspaceRepository>()
                .AddAsync(otherWorkspace);
            await scope.ServiceProvider
                .GetRequiredService<IWorkspaceMemberRepository>()
                .AddAsync(membership);

            var feedbackRepository = scope.ServiceProvider.GetRequiredService<IFeedbackRepository>();
            await feedbackRepository.AddAsync(source);
            await feedbackRepository.AddAsync(closeMatch);
            await feedbackRepository.AddAsync(belowThreshold);
            await feedbackRepository.AddAsync(crossWorkspaceMatch);

            var embeddingRepository = scope.ServiceProvider
                .GetRequiredService<IFeedbackEmbeddingRepository>();
            await embeddingRepository.AddAsync(sourceEmbedding);
            await embeddingRepository.AddAsync(closeEmbedding);
            await embeddingRepository.AddAsync(belowThresholdEmbedding);
            await embeddingRepository.AddAsync(crossWorkspaceEmbedding);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        }

        await using (var serviceProvider = database.CreateServiceProvider())
        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider
                .GetRequiredService<IFeedbackEmbeddingRepository>();
            var persisted = await repository.GetByFeedbackIdAsync(
                workspace.Id,
                source.Id);
            var crossWorkspaceRead = await repository.GetByFeedbackIdAsync(
                otherWorkspace.Id,
                source.Id);
            var matches = await repository.FindSimilarAsync(
                workspace.Id,
                source.Id,
                minimumSimilarity: 0.9,
                limit: 10);

            Assert.NotNull(persisted);
            Assert.Equal(FeedbackEmbedding.Dimensions, persisted.Values.Count);
            Assert.Equal(1f, persisted.Values[0]);
            Assert.Null(crossWorkspaceRead);

            var match = Assert.Single(matches);
            Assert.Equal(closeMatch.Id, match.FeedbackId);
            Assert.True(match.Similarity > 0.99);
            Assert.Equal(closeMatch.Title, match.Title);
        }
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
        var leaseId = feedback.StartProcessing(createdAt.AddSeconds(1));
        feedback.CompleteProcessing(leaseId, createdAt.AddSeconds(2));

        return feedback;
    }

    private static FeedbackEmbedding CreateEmbedding(
        FeedbackEntity feedback,
        float[] values,
        char hashCharacter,
        DateTimeOffset embeddedAt)
    {
        return FeedbackEmbedding.Create(
            feedback.WorkspaceId,
            feedback.Id,
            values,
            "integration-test-model",
            new string(hashCharacter, FeedbackEmbedding.SourceHashLength),
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
