using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Domain.Feedback;
using PulsePilot.Domain.Users;
using PulsePilot.Domain.Workspaces;

using FeedbackEntity = PulsePilot.Domain.Feedback.Feedback;

namespace PulsePilot.IntegrationTests.Persistence;

public sealed class FeedbackClusterRepositoryTests(PostgreSqlFixture database)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Repository_ReturnsWorkspaceScopedSummariesAndActiveMembers()
    {
        var now = DateTimeOffset.UtcNow;
        var user = User.Create(
            $"cluster-{Guid.CreateVersion7():N}@example.com",
            "Cluster Owner",
            "password-hash",
            now);
        var workspace = Workspace.Create("Cluster Workspace", now);
        var otherWorkspace = Workspace.Create("Other Cluster Workspace", now);
        var membership = WorkspaceMember.Join(
            workspace.Id,
            user.Id,
            WorkspaceRole.Admin,
            now);
        var cluster = FeedbackCluster.Create(
            workspace.Id,
            "Payment failures",
            FeedbackCategory.Bug,
            FeedbackComponent.Payments,
            now);
        var otherCluster = FeedbackCluster.Create(
            otherWorkspace.Id,
            "Other payment failures",
            FeedbackCategory.Bug,
            FeedbackComponent.Payments,
            now);
        var activeFeedback = CreateFeedback(workspace.Id, user.Id, "Active payment report", now);
        var deletedFeedback = CreateFeedback(
            workspace.Id,
            user.Id,
            "Deleted payment report",
            now.AddSeconds(1));
        activeFeedback.AssignToCluster(cluster.Id, now.AddMinutes(1));
        deletedFeedback.AssignToCluster(cluster.Id, now.AddMinutes(1));
        deletedFeedback.MarkDeleted(now.AddMinutes(2));
        cluster.RecordActivity(now.AddMinutes(1));

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
            var clusterRepository = scope.ServiceProvider
                .GetRequiredService<IFeedbackClusterRepository>();
            await clusterRepository.AddAsync(cluster);
            await clusterRepository.AddAsync(otherCluster);
            var feedbackRepository = scope.ServiceProvider.GetRequiredService<IFeedbackRepository>();
            await feedbackRepository.AddAsync(activeFeedback);
            await feedbackRepository.AddAsync(deletedFeedback);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        }

        await using (var serviceProvider = database.CreateServiceProvider())
        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IFeedbackClusterRepository>();
            var summaries = await repository.ListSummariesAsync(
                workspace.Id,
                skip: 0,
                take: 10);
            var totalCount = await repository.CountAsync(workspace.Id);
            var members = await repository.ListMembersAsync(
                workspace.Id,
                cluster.Id,
                skip: 0,
                take: 10);
            var memberCount = await repository.CountMembersAsync(workspace.Id, cluster.Id);
            var crossWorkspace = await repository.GetByIdAsync(
                otherWorkspace.Id,
                cluster.Id);

            var summary = Assert.Single(summaries);
            Assert.Equal(cluster.Id, summary.Id);
            Assert.Equal(1, summary.FeedbackCount);
            Assert.Equal(1, totalCount);
            Assert.Equal(1, memberCount);
            Assert.Equal(activeFeedback.Id, Assert.Single(members).Id);
            Assert.Null(crossWorkspace);
        }

        await using (var serviceProvider = database.CreateServiceProvider())
        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var mismatchedFeedback = CreateFeedback(
                workspace.Id,
                user.Id,
                "Mismatched tenant cluster",
                now.AddMinutes(3));
            mismatchedFeedback.AssignToCluster(otherCluster.Id, now.AddMinutes(4));
            await scope.ServiceProvider
                .GetRequiredService<IFeedbackRepository>()
                .AddAsync(mismatchedFeedback);

            await Assert.ThrowsAsync<DbUpdateException>(() =>
                scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync());
        }
    }

    private static FeedbackEntity CreateFeedback(
        Guid workspaceId,
        Guid userId,
        string title,
        DateTimeOffset createdAt)
    {
        return FeedbackEntity.Create(
            workspaceId,
            userId,
            title,
            $"{title} content",
            FeedbackSource.Manual,
            null,
            null,
            createdAt);
    }
}
