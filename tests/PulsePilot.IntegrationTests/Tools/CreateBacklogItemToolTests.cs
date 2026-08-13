using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PulsePilot.Application;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Application.Common.Exceptions;
using PulsePilot.Application.Tools;
using PulsePilot.Domain.Actions;
using PulsePilot.Domain.Feedback;
using PulsePilot.Domain.Users;
using PulsePilot.Domain.Workspaces;
using PulsePilot.Infrastructure.Persistence;
using PulsePilot.IntegrationTests.Persistence;

using FeedbackEntity = PulsePilot.Domain.Feedback.Feedback;

namespace PulsePilot.IntegrationTests.Tools;

public sealed class CreateBacklogItemToolTests(PostgreSqlFixture database)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Tool_RejectsWrongTypeAndUnapprovedActionWithoutSideEffects()
    {
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        var user = User.Create(
            $"tool-guard-{Guid.CreateVersion7():N}@example.com",
            "Tool Owner",
            "password-hash",
            now);
        var workspace = Workspace.Create("Tool Guard Workspace", now);
        var membership = WorkspaceMember.Join(
            workspace.Id,
            user.Id,
            WorkspaceRole.Admin,
            now);
        var customerResponse = CreateActionGraph(
            workspace.Id,
            user.Id,
            PendingActionType.DraftCustomerResponse,
            "Customer response",
            now);
        customerResponse.Action.Approve(now.AddMinutes(1));
        var unapprovedEngineeringIssue = CreateActionGraph(
            workspace.Id,
            user.Id,
            PendingActionType.CreateEngineeringIssue,
            "Engineering issue",
            now.AddSeconds(1));

        await using var serviceProvider = database.CreateServiceProvider(
            configureServices: services => services.AddApplication());
        await using var scope = serviceProvider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IUserRepository>().AddAsync(user);
        await scope.ServiceProvider.GetRequiredService<IWorkspaceRepository>().AddAsync(workspace);
        await scope.ServiceProvider
            .GetRequiredService<IWorkspaceMemberRepository>()
            .AddAsync(membership);
        var clusterRepository = scope.ServiceProvider
            .GetRequiredService<IFeedbackClusterRepository>();
        var feedbackRepository = scope.ServiceProvider.GetRequiredService<IFeedbackRepository>();
        var actionRepository = scope.ServiceProvider
            .GetRequiredService<IPendingActionRepository>();
        await clusterRepository.AddAsync(customerResponse.Cluster);
        await clusterRepository.AddAsync(unapprovedEngineeringIssue.Cluster);
        await feedbackRepository.AddAsync(customerResponse.Feedback);
        await feedbackRepository.AddAsync(unapprovedEngineeringIssue.Feedback);
        await actionRepository.AddAsync(customerResponse.Action);
        await actionRepository.AddAsync(unapprovedEngineeringIssue.Action);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        var tool = scope.ServiceProvider.GetRequiredService<ICreateBacklogItemTool>();

        await Assert.ThrowsAsync<ConflictException>(() => tool.ExecuteAsync(
            customerResponse.Action,
            user.Id,
            now.AddMinutes(2)));
        await Assert.ThrowsAsync<ConflictException>(() => tool.ExecuteAsync(
            unapprovedEngineeringIssue.Action,
            user.Id,
            now.AddMinutes(2)));

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await dbContext.BacklogItems.AsNoTracking().ToListAsync());
        Assert.Equal(PendingActionStatus.Approved, customerResponse.Action.Status);
        Assert.Equal(PendingActionStatus.Pending, unapprovedEngineeringIssue.Action.Status);
    }

    private static ActionGraph CreateActionGraph(
        Guid workspaceId,
        Guid userId,
        PendingActionType actionType,
        string title,
        DateTimeOffset createdAt)
    {
        var cluster = FeedbackCluster.Create(
            workspaceId,
            title,
            FeedbackCategory.Bug,
            FeedbackComponent.Payments,
            createdAt);
        cluster.UpdatePriority(80m, FeedbackPriority.P1, createdAt.AddSeconds(1));
        var feedback = FeedbackEntity.Create(
            workspaceId,
            userId,
            title,
            $"{title} feedback content.",
            FeedbackSource.Manual,
            null,
            null,
            createdAt);
        feedback.AssignToCluster(cluster.Id, createdAt.AddSeconds(1));
        var action = PendingAction.Create(
            workspaceId,
            feedback.Id,
            cluster.Id,
            actionType,
            $"[P1] {title}",
            $"{title} action description.",
            "{\"priority\":\"p1\"}",
            createdAt.AddSeconds(1));

        return new ActionGraph(cluster, feedback, action);
    }

    private sealed record ActionGraph(
        FeedbackCluster Cluster,
        FeedbackEntity Feedback,
        PendingAction Action);
}
