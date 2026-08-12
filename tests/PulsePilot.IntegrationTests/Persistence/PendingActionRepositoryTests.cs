using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Domain.Actions;
using PulsePilot.Domain.Feedback;
using PulsePilot.Domain.Users;
using PulsePilot.Domain.Workspaces;

using FeedbackEntity = PulsePilot.Domain.Feedback.Feedback;

namespace PulsePilot.IntegrationTests.Persistence;

public sealed class PendingActionRepositoryTests(PostgreSqlFixture database)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Repository_IsWorkspaceScopedAndPreventsDuplicateActiveRecommendation()
    {
        var now = DateTimeOffset.UtcNow;
        var user = User.Create(
            $"pending-action-{Guid.CreateVersion7():N}@example.com",
            "Action Owner",
            "password-hash",
            now);
        var workspace = Workspace.Create("Action Workspace", now);
        var otherWorkspace = Workspace.Create("Other Action Workspace", now);
        var membership = WorkspaceMember.Join(
            workspace.Id,
            user.Id,
            WorkspaceRole.Admin,
            now);
        var cluster = CreateCluster(workspace.Id, "Payment failures", now);
        var otherCluster = CreateCluster(otherWorkspace.Id, "Other failures", now);
        var feedback = FeedbackEntity.Create(
            workspace.Id,
            user.Id,
            "Card payment fails",
            "The checkout rejects every payment card.",
            FeedbackSource.Manual,
            null,
            null,
            now);
        feedback.AssignToCluster(cluster.Id, now);
        var pendingAction = CreatePendingAction(
            workspace.Id,
            feedback.Id,
            cluster.Id,
            now);

        await using (var serviceProvider = database.CreateServiceProvider())
        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IUserRepository>().AddAsync(user);
            var workspaceRepository = scope.ServiceProvider
                .GetRequiredService<IWorkspaceRepository>();
            await workspaceRepository.AddAsync(workspace);
            await workspaceRepository.AddAsync(otherWorkspace);
            await scope.ServiceProvider
                .GetRequiredService<IWorkspaceMemberRepository>()
                .AddAsync(membership);
            var clusterRepository = scope.ServiceProvider
                .GetRequiredService<IFeedbackClusterRepository>();
            await clusterRepository.AddAsync(cluster);
            await clusterRepository.AddAsync(otherCluster);
            await scope.ServiceProvider
                .GetRequiredService<IFeedbackRepository>()
                .AddAsync(feedback);
            await scope.ServiceProvider
                .GetRequiredService<IPendingActionRepository>()
                .AddAsync(pendingAction);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        }

        await using (var serviceProvider = database.CreateServiceProvider())
        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider
                .GetRequiredService<IPendingActionRepository>();
            var list = await repository.ListAsync(
                workspace.Id,
                PendingActionStatus.Pending,
                skip: 0,
                take: 10);
            var count = await repository.CountAsync(
                workspace.Id,
                PendingActionStatus.Pending);
            var detail = await repository.GetByIdAsync(workspace.Id, pendingAction.Id);
            var active = await repository.GetActiveByClusterAndTypeAsync(
                workspace.Id,
                cluster.Id,
                PendingActionType.CreateEngineeringIssue);
            var crossWorkspace = await repository.GetByIdAsync(
                otherWorkspace.Id,
                pendingAction.Id);

            Assert.Equal(1, count);
            Assert.Equal(pendingAction.Id, Assert.Single(list).Id);
            Assert.Equal(pendingAction.Id, detail?.Id);
            Assert.Equal(pendingAction.Id, active?.Id);
            Assert.Null(crossWorkspace);
        }

        await using (var serviceProvider = database.CreateServiceProvider())
        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var duplicate = CreatePendingAction(
                workspace.Id,
                feedback.Id,
                cluster.Id,
                now.AddSeconds(1));
            await scope.ServiceProvider
                .GetRequiredService<IPendingActionRepository>()
                .AddAsync(duplicate);

            await Assert.ThrowsAsync<DbUpdateException>(() =>
                scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync());
        }

        await using (var serviceProvider = database.CreateServiceProvider())
        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var mismatchedClusterAction = CreatePendingAction(
                workspace.Id,
                feedback.Id,
                otherCluster.Id,
                now.AddSeconds(2));
            await scope.ServiceProvider
                .GetRequiredService<IPendingActionRepository>()
                .AddAsync(mismatchedClusterAction);

            await Assert.ThrowsAsync<DbUpdateException>(() =>
                scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync());
        }
    }

    private static FeedbackCluster CreateCluster(
        Guid workspaceId,
        string title,
        DateTimeOffset createdAt)
    {
        return FeedbackCluster.Create(
            workspaceId,
            title,
            FeedbackCategory.Bug,
            FeedbackComponent.Payments,
            createdAt);
    }

    private static PendingAction CreatePendingAction(
        Guid workspaceId,
        Guid feedbackId,
        Guid clusterId,
        DateTimeOffset createdAt)
    {
        return PendingAction.Create(
            workspaceId,
            feedbackId,
            clusterId,
            PendingActionType.CreateEngineeringIssue,
            "[P1] Payment failures",
            "Create an engineering issue for this cluster.",
            "{\"priority\":\"p1\"}",
            createdAt);
    }
}
