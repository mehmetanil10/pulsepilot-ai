using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Domain.Feedback;
using PulsePilot.Domain.Users;
using PulsePilot.Domain.Workspaces;

using FeedbackEntity = PulsePilot.Domain.Feedback.Feedback;

namespace PulsePilot.IntegrationTests.Persistence;

public sealed class FeedbackAnalysisRepositoryTests(PostgreSqlFixture database)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Repository_PersistsAnalysisAndEnforcesWorkspaceScope()
    {
        var now = DateTimeOffset.UtcNow;
        var user = User.Create(
            $"analysis-{Guid.CreateVersion7():N}@example.com",
            "Analysis Owner",
            "password-hash",
            now);
        var workspace = Workspace.Create("Analysis Workspace", now);
        var membership = WorkspaceMember.Join(
            workspace.Id,
            user.Id,
            WorkspaceRole.Admin,
            now);
        var feedback = FeedbackEntity.Create(
            workspace.Id,
            user.Id,
            "Card cannot be added",
            "After the latest update I cannot add my credit card.",
            FeedbackSource.Manual,
            null,
            null,
            now);
        var analysis = FeedbackAnalysis.Create(
            workspace.Id,
            feedback.Id,
            FeedbackCategory.Bug,
            FeedbackComponent.Payments,
            4,
            FeedbackSentiment.Negative,
            "User cannot add a payment card.",
            "Investigate the payment service.",
            0.94m,
            now.AddMinutes(1));

        await using (var serviceProvider = database.CreateServiceProvider())
        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IUserRepository>().AddAsync(user);
            await scope.ServiceProvider.GetRequiredService<IWorkspaceRepository>().AddAsync(workspace);
            await scope.ServiceProvider
                .GetRequiredService<IWorkspaceMemberRepository>()
                .AddAsync(membership);
            await scope.ServiceProvider.GetRequiredService<IFeedbackRepository>().AddAsync(feedback);
            await scope.ServiceProvider
                .GetRequiredService<IFeedbackAnalysisRepository>()
                .AddAsync(analysis);

            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        }

        await using (var serviceProvider = database.CreateServiceProvider())
        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider
                .GetRequiredService<IFeedbackAnalysisRepository>();
            var persisted = await repository.GetByFeedbackIdAsync(
                workspace.Id,
                feedback.Id);
            var crossWorkspace = await repository.GetByFeedbackIdAsync(
                Guid.CreateVersion7(),
                feedback.Id);

            Assert.NotNull(persisted);
            Assert.Equal(FeedbackCategory.Bug, persisted.Category);
            Assert.Equal(FeedbackComponent.Payments, persisted.Component);
            Assert.Equal(4, persisted.Severity);
            Assert.Equal(FeedbackSentiment.Negative, persisted.Sentiment);
            Assert.Equal(0.94m, persisted.Confidence);
            Assert.Null(crossWorkspace);
        }

        await using (var serviceProvider = database.CreateServiceProvider())
        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var otherWorkspace = Workspace.Create("Other Workspace", now.AddMinutes(2));
            var mismatchedAnalysis = FeedbackAnalysis.Create(
                otherWorkspace.Id,
                feedback.Id,
                FeedbackCategory.Bug,
                FeedbackComponent.Payments,
                4,
                FeedbackSentiment.Negative,
                "This analysis points to feedback in another workspace.",
                "Reject the mismatched tenant reference.",
                0.9m,
                now.AddMinutes(3));

            await scope.ServiceProvider
                .GetRequiredService<IWorkspaceRepository>()
                .AddAsync(otherWorkspace);
            await scope.ServiceProvider
                .GetRequiredService<IFeedbackAnalysisRepository>()
                .AddAsync(mismatchedAnalysis);

            await Assert.ThrowsAsync<DbUpdateException>(() =>
                scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync());
        }
    }
}
