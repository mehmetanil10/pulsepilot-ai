using Microsoft.Extensions.DependencyInjection;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Domain.Feedback;
using PulsePilot.Domain.Users;
using PulsePilot.Domain.Workspaces;

using FeedbackEntity = PulsePilot.Domain.Feedback.Feedback;

namespace PulsePilot.IntegrationTests.Persistence;

public sealed class RepositoryPersistenceTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public RepositoryPersistenceTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Repositories_PersistGraphAndEnforceWorkspaceScopeAndSoftDelete()
    {
        var now = DateTimeOffset.UtcNow;
        var email = $"user-{Guid.CreateVersion7():N}@example.com";
        var user = User.Create(email, "Integration User", "password-hash", now);
        var workspace = Workspace.Create("Integration Workspace", now);
        var membership = WorkspaceMember.Join(
            workspace.Id,
            user.Id,
            WorkspaceRole.Admin,
            now);
        var feedback = FeedbackEntity.Create(
            workspace.Id,
            user.Id,
            "Payment problem",
            "I cannot add my card.",
            FeedbackSource.Api,
            "Integration Customer",
            "customer@example.com",
            now);

        await using (var serviceProvider = _fixture.CreateServiceProvider())
        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IUserRepository>().AddAsync(user);
            await scope.ServiceProvider.GetRequiredService<IWorkspaceRepository>().AddAsync(workspace);
            await scope.ServiceProvider
                .GetRequiredService<IWorkspaceMemberRepository>()
                .AddAsync(membership);
            await scope.ServiceProvider.GetRequiredService<IFeedbackRepository>().AddAsync(feedback);

            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        }

        await using (var serviceProvider = _fixture.CreateServiceProvider())
        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var workspaceRepository = scope.ServiceProvider.GetRequiredService<IWorkspaceRepository>();
            var memberRepository = scope.ServiceProvider.GetRequiredService<IWorkspaceMemberRepository>();
            var feedbackRepository = scope.ServiceProvider.GetRequiredService<IFeedbackRepository>();

            Assert.True(await userRepository.ExistsByNormalizedEmailAsync(user.NormalizedEmail));
            Assert.NotNull(await workspaceRepository.GetByIdAsync(workspace.Id));
            Assert.Single(await memberRepository.ListByUserIdAsync(user.Id));
            Assert.Null(await feedbackRepository.GetByIdAsync(Guid.CreateVersion7(), feedback.Id));

            var persistedFeedback = await feedbackRepository.GetByIdAsync(workspace.Id, feedback.Id);
            var filteredFeedback = await feedbackRepository.ListAsync(
                workspace.Id,
                skip: 0,
                take: 10,
                source: FeedbackSource.Api,
                processingStatus: ProcessingStatus.Pending);

            Assert.NotNull(persistedFeedback);
            Assert.Single(filteredFeedback);

            persistedFeedback.MarkDeleted(now.AddMinutes(1));
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        }

        await using (var serviceProvider = _fixture.CreateServiceProvider())
        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var feedbackRepository = scope.ServiceProvider.GetRequiredService<IFeedbackRepository>();

            Assert.Null(await feedbackRepository.GetByIdAsync(workspace.Id, feedback.Id));
        }
    }
}
