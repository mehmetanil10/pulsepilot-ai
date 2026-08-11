using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Domain.Common;
using PulsePilot.Domain.Feedback;
using PulsePilot.Domain.Users;
using PulsePilot.Domain.Workspaces;
using PulsePilot.Infrastructure.Persistence;

using FeedbackEntity = PulsePilot.Domain.Feedback.Feedback;

namespace PulsePilot.IntegrationTests.Persistence;

public sealed class FeedbackProcessingQueueTests(PostgreSqlFixture database)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Queue_ClaimsDistinctRowsAndInvalidatesRecoveredLease()
    {
        var now = DateTimeOffset.UtcNow;
        var (workspace, firstFeedback, secondFeedback) = await SeedFeedbackAsync(now);
        await using var firstProvider = database.CreateServiceProvider();
        await using var secondProvider = database.CreateServiceProvider();

        var firstClaimTask = ClaimAsync(firstProvider, now.AddMinutes(1));
        var secondClaimTask = ClaimAsync(secondProvider, now.AddMinutes(1));
        var claims = await Task.WhenAll(firstClaimTask, secondClaimTask);

        Assert.All(claims, Assert.NotNull);
        Assert.Equal(2, claims.Select(claim => claim!.FeedbackId).Distinct().Count());
        Assert.Contains(claims, claim => claim!.FeedbackId == firstFeedback.Id);
        Assert.Contains(claims, claim => claim!.FeedbackId == secondFeedback.Id);

        await using var recoveryProvider = database.CreateServiceProvider();
        await using var recoveryScope = recoveryProvider.CreateAsyncScope();
        var recoveredCount = await recoveryScope.ServiceProvider
            .GetRequiredService<IFeedbackProcessingQueue>()
            .RecoverStaleAsync(
                now.AddMinutes(2),
                now.AddMinutes(3),
                maxCount: 10);

        Assert.Equal(2, recoveredCount);

        await using var verificationProvider = database.CreateServiceProvider();
        await using var verificationScope = verificationProvider.CreateAsyncScope();
        var dbContext = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var targetFeedbackId = claims[0]!.FeedbackId;
        var recoveredFeedback = await dbContext.Feedback
            .SingleAsync(feedback => feedback.Id == targetFeedbackId);
        var expiredClaim = claims.Single(
            claim => claim!.FeedbackId == recoveredFeedback.Id)!;

        Assert.Equal(ProcessingStatus.Pending, recoveredFeedback.ProcessingStatus);
        Assert.Null(recoveredFeedback.ProcessingLeaseId);
        Assert.Null(recoveredFeedback.ProcessingStartedAt);
        Assert.Throws<DomainException>(() =>
            recoveredFeedback.CompleteProcessing(
                expiredClaim.ProcessingLeaseId,
                now.AddMinutes(4)));

        await using var reclaimProvider = database.CreateServiceProvider();
        var reclaimed = await ClaimAsync(reclaimProvider, now.AddMinutes(4));

        Assert.NotNull(reclaimed);
        Assert.NotEqual(expiredClaim.ProcessingLeaseId, reclaimed.ProcessingLeaseId);
    }

    private static async Task<PulsePilot.Application.FeedbackProcessing.FeedbackProcessingItem?>
        ClaimAsync(
            ServiceProvider serviceProvider,
            DateTimeOffset claimedAt)
    {
        await using var scope = serviceProvider.CreateAsyncScope();

        return await scope.ServiceProvider
            .GetRequiredService<IFeedbackProcessingQueue>()
            .ClaimNextPendingAsync(claimedAt);
    }

    private async Task<(Workspace Workspace, FeedbackEntity First, FeedbackEntity Second)>
        SeedFeedbackAsync(DateTimeOffset now)
    {
        var user = User.Create(
            $"queue-{Guid.CreateVersion7():N}@example.com",
            "Queue Owner",
            "password-hash",
            now);
        var workspace = Workspace.Create("Queue Workspace", now);
        var membership = WorkspaceMember.Join(
            workspace.Id,
            user.Id,
            WorkspaceRole.Admin,
            now);
        var firstFeedback = FeedbackEntity.Create(
            workspace.Id,
            user.Id,
            "First queue item",
            "The first feedback waits for processing.",
            FeedbackSource.Manual,
            null,
            null,
            now);
        var secondFeedback = FeedbackEntity.Create(
            workspace.Id,
            user.Id,
            "Second queue item",
            "The second feedback waits for processing.",
            FeedbackSource.Api,
            null,
            null,
            now.AddSeconds(1));

        await using var serviceProvider = database.CreateServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IUserRepository>().AddAsync(user);
        await scope.ServiceProvider.GetRequiredService<IWorkspaceRepository>().AddAsync(workspace);
        await scope.ServiceProvider
            .GetRequiredService<IWorkspaceMemberRepository>()
            .AddAsync(membership);
        await scope.ServiceProvider
            .GetRequiredService<IFeedbackRepository>()
            .AddAsync(firstFeedback);
        await scope.ServiceProvider
            .GetRequiredService<IFeedbackRepository>()
            .AddAsync(secondFeedback);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

        return (workspace, firstFeedback, secondFeedback);
    }
}
