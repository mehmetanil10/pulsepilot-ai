using Microsoft.Extensions.DependencyInjection;
using PulsePilot.Application;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Application.Tools;
using PulsePilot.Domain.Feedback;
using PulsePilot.Domain.Users;
using PulsePilot.Domain.Workspaces;
using PulsePilot.IntegrationTests.Persistence;

using FeedbackEntity = PulsePilot.Domain.Feedback.Feedback;

namespace PulsePilot.IntegrationTests.Tools;

public sealed class GetTrendingIssuesToolTests(PostgreSqlFixture database)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly DateTimeOffset CurrentTime = new(
        2026,
        8,
        13,
        12,
        0,
        0,
        TimeSpan.Zero);

    private static readonly IReadOnlyDictionary<string, string?> TrendingConfiguration =
        new Dictionary<string, string?>
        {
            ["TrendingIssues:DefaultPeriodDays"] = "7",
            ["TrendingIssues:MaxPeriodDays"] = "30",
            ["TrendingIssues:DefaultLimit"] = "2",
            ["TrendingIssues:MaxLimit"] = "10",
        };

    [Fact]
    public async Task Tool_ReturnsOnlyGrowingWorkspaceClustersOrderedByAbsoluteIncrease()
    {
        var createdAt = CurrentTime.AddDays(-40);
        var user = User.Create(
            $"trending-tool-{Guid.CreateVersion7():N}@example.com",
            "Trending Tool Owner",
            "password-hash",
            createdAt);
        var workspace = Workspace.Create("Trending Tool Workspace", createdAt);
        var otherWorkspace = Workspace.Create("Other Trending Workspace", createdAt);
        var payment = CreateCluster(
            workspace.Id,
            "Payment failures",
            FeedbackCategory.Bug,
            FeedbackComponent.Payments,
            90m,
            FeedbackPriority.P1,
            createdAt);
        var authentication = CreateCluster(
            workspace.Id,
            "Authentication timeouts",
            FeedbackCategory.Bug,
            FeedbackComponent.Authentication,
            70m,
            FeedbackPriority.P2,
            createdAt);
        var newIssue = CreateCluster(
            workspace.Id,
            "New dashboard regression",
            FeedbackCategory.Bug,
            FeedbackComponent.Dashboard,
            45m,
            FeedbackPriority.P3,
            createdAt);
        var stable = CreateCluster(
            workspace.Id,
            "Stable reporting issue",
            FeedbackCategory.Complaint,
            FeedbackComponent.Reporting,
            40m,
            FeedbackPriority.P3,
            createdAt);
        var declining = CreateCluster(
            workspace.Id,
            "Declining mobile issue",
            FeedbackCategory.Complaint,
            FeedbackComponent.Mobile,
            30m,
            FeedbackPriority.P3,
            createdAt);
        var previousOnly = CreateCluster(
            workspace.Id,
            "Resolved API spike",
            FeedbackCategory.Bug,
            FeedbackComponent.Api,
            20m,
            FeedbackPriority.P4,
            createdAt);
        var crossWorkspace = CreateCluster(
            otherWorkspace.Id,
            "Cross workspace incident",
            FeedbackCategory.Bug,
            FeedbackComponent.Payments,
            100m,
            FeedbackPriority.P1,
            createdAt);
        var feedback = new List<FeedbackEntity>();

        AddFeedback(feedback, workspace.Id, user.Id, payment.Id, CurrentTime.AddDays(-14));
        AddFeedback(
            feedback,
            workspace.Id,
            user.Id,
            payment.Id,
            CurrentTime.AddDays(-7).AddTicks(-1));

        foreach (var daysAgo in new[] { 7, 6, 5, 4, 3, 2, 1 })
        {
            AddFeedback(
                feedback,
                workspace.Id,
                user.Id,
                payment.Id,
                CurrentTime.AddDays(-daysAgo));
        }

        AddFeedback(
            feedback,
            workspace.Id,
            user.Id,
            payment.Id,
            CurrentTime.AddDays(-14).AddTicks(-1));
        AddFeedback(feedback, workspace.Id, user.Id, payment.Id, CurrentTime);
        AddFeedback(
            feedback,
            workspace.Id,
            user.Id,
            payment.Id,
            CurrentTime.AddHours(-12),
            deleted: true);

        AddFeedback(feedback, workspace.Id, user.Id, authentication.Id, CurrentTime.AddDays(-10));

        foreach (var daysAgo in new[] { 6, 4, 2, 1 })
        {
            AddFeedback(
                feedback,
                workspace.Id,
                user.Id,
                authentication.Id,
                CurrentTime.AddDays(-daysAgo));
        }

        AddFeedback(feedback, workspace.Id, user.Id, newIssue.Id, CurrentTime.AddDays(-3));
        AddFeedback(feedback, workspace.Id, user.Id, newIssue.Id, CurrentTime.AddDays(-1));
        AddFeedback(feedback, workspace.Id, user.Id, stable.Id, CurrentTime.AddDays(-12));
        AddFeedback(feedback, workspace.Id, user.Id, stable.Id, CurrentTime.AddDays(-8));
        AddFeedback(feedback, workspace.Id, user.Id, stable.Id, CurrentTime.AddDays(-5));
        AddFeedback(feedback, workspace.Id, user.Id, stable.Id, CurrentTime.AddDays(-1));
        AddFeedback(feedback, workspace.Id, user.Id, declining.Id, CurrentTime.AddDays(-13));
        AddFeedback(feedback, workspace.Id, user.Id, declining.Id, CurrentTime.AddDays(-10));
        AddFeedback(feedback, workspace.Id, user.Id, declining.Id, CurrentTime.AddDays(-8));
        AddFeedback(feedback, workspace.Id, user.Id, declining.Id, CurrentTime.AddDays(-1));
        AddFeedback(feedback, workspace.Id, user.Id, previousOnly.Id, CurrentTime.AddDays(-9));
        AddFeedback(feedback, workspace.Id, user.Id, previousOnly.Id, CurrentTime.AddDays(-8));

        for (var index = 1; index <= 10; index++)
        {
            AddFeedback(
                feedback,
                otherWorkspace.Id,
                user.Id,
                crossWorkspace.Id,
                CurrentTime.AddHours(-index));
        }

        AddFeedback(
            feedback,
            workspace.Id,
            user.Id,
            clusterId: null,
            CurrentTime.AddHours(-1));

        await using var serviceProvider = CreateServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();
        await SeedAsync(
            scope.ServiceProvider,
            user,
            [workspace, otherWorkspace],
            [payment, authentication, newIssue, stable, declining, previousOnly, crossWorkspace],
            feedback);
        var tool = scope.ServiceProvider.GetRequiredService<IGetTrendingIssuesTool>();

        var defaultResult = await tool.ExecuteAsync(
            workspace.Id,
            new GetTrendingIssuesToolInput());
        var expandedResult = await tool.ExecuteAsync(
            workspace.Id,
            new GetTrendingIssuesToolInput(Limit: 10));

        Assert.Equal(CurrentTime.AddDays(-14), defaultResult.PreviousFromInclusive);
        Assert.Equal(CurrentTime.AddDays(-7), defaultResult.CurrentFromInclusive);
        Assert.Equal(CurrentTime, defaultResult.CurrentToExclusive);
        Assert.Equal(7, defaultResult.PeriodDays);
        Assert.Equal([payment.Id, authentication.Id], defaultResult.Items.Select(item => item.FeedbackClusterId));
        Assert.Equal(3, expandedResult.Count);
        Assert.Equal(
            [payment.Id, authentication.Id, newIssue.Id],
            expandedResult.Items.Select(item => item.FeedbackClusterId));

        var paymentTrend = expandedResult.Items[0];
        Assert.Equal(7, paymentTrend.CurrentPeriodCount);
        Assert.Equal(2, paymentTrend.PreviousPeriodCount);
        Assert.Equal(5, paymentTrend.DeltaCount);
        Assert.Equal(250m, paymentTrend.GrowthPercentage);
        Assert.False(paymentTrend.IsNew);
        Assert.Equal(FeedbackPriority.P1, paymentTrend.Priority);
        Assert.Equal(90m, paymentTrend.PriorityScore);

        var authenticationTrend = expandedResult.Items[1];
        Assert.Equal(4, authenticationTrend.CurrentPeriodCount);
        Assert.Equal(1, authenticationTrend.PreviousPeriodCount);
        Assert.Equal(3, authenticationTrend.DeltaCount);
        Assert.Equal(300m, authenticationTrend.GrowthPercentage);
        Assert.False(authenticationTrend.IsNew);

        var newTrend = expandedResult.Items[2];
        Assert.Equal(2, newTrend.CurrentPeriodCount);
        Assert.Equal(0, newTrend.PreviousPeriodCount);
        Assert.Equal(2, newTrend.DeltaCount);
        Assert.Null(newTrend.GrowthPercentage);
        Assert.True(newTrend.IsNew);
        Assert.DoesNotContain(
            expandedResult.Items,
            item => item.FeedbackClusterId == stable.Id
                || item.FeedbackClusterId == declining.Id
                || item.FeedbackClusterId == previousOnly.Id
                || item.FeedbackClusterId == crossWorkspace.Id);
    }

    [Fact]
    public async Task Tool_ReturnsEmptyResultAndRejectsInvalidUntrustedInputs()
    {
        await using var serviceProvider = CreateServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();
        var tool = scope.ServiceProvider.GetRequiredService<IGetTrendingIssuesTool>();
        var workspaceId = Guid.CreateVersion7();

        var result = await tool.ExecuteAsync(
            workspaceId,
            new GetTrendingIssuesToolInput());

        Assert.Empty(result.Items);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => tool.ExecuteAsync(
            workspaceId,
            new GetTrendingIssuesToolInput(PeriodDays: 0)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => tool.ExecuteAsync(
            workspaceId,
            new GetTrendingIssuesToolInput(PeriodDays: 31)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => tool.ExecuteAsync(
            workspaceId,
            new GetTrendingIssuesToolInput(Limit: 0)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => tool.ExecuteAsync(
            workspaceId,
            new GetTrendingIssuesToolInput(Limit: 11)));
        await Assert.ThrowsAsync<ArgumentException>(() => tool.ExecuteAsync(
            Guid.Empty,
            new GetTrendingIssuesToolInput()));
        await Assert.ThrowsAsync<ArgumentNullException>(() => tool.ExecuteAsync(
            workspaceId,
            null!));
    }

    private ServiceProvider CreateServiceProvider()
    {
        return database.CreateServiceProvider(
            TrendingConfiguration,
            services =>
            {
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(CurrentTime));
                services.AddApplication();
            });
    }

    private static async Task SeedAsync(
        IServiceProvider serviceProvider,
        User user,
        IReadOnlyList<Workspace> workspaces,
        IReadOnlyList<FeedbackCluster> clusters,
        IReadOnlyList<FeedbackEntity> feedback)
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

        var clusterRepository = serviceProvider
            .GetRequiredService<IFeedbackClusterRepository>();

        foreach (var cluster in clusters)
        {
            await clusterRepository.AddAsync(cluster);
        }

        var feedbackRepository = serviceProvider.GetRequiredService<IFeedbackRepository>();

        foreach (var item in feedback)
        {
            await feedbackRepository.AddAsync(item);
        }

        await serviceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
    }

    private static FeedbackCluster CreateCluster(
        Guid workspaceId,
        string title,
        FeedbackCategory category,
        FeedbackComponent component,
        decimal priorityScore,
        FeedbackPriority priority,
        DateTimeOffset createdAt)
    {
        var cluster = FeedbackCluster.Create(
            workspaceId,
            title,
            category,
            component,
            createdAt);
        cluster.UpdatePriority(priorityScore, priority, createdAt.AddMilliseconds(1));

        return cluster;
    }

    private static void AddFeedback(
        ICollection<FeedbackEntity> feedback,
        Guid workspaceId,
        Guid userId,
        Guid? clusterId,
        DateTimeOffset createdAt,
        bool deleted = false)
    {
        var item = FeedbackEntity.Create(
            workspaceId,
            userId,
            "Trend test feedback",
            "Trend test feedback content.",
            FeedbackSource.Manual,
            null,
            null,
            createdAt);

        if (clusterId.HasValue)
        {
            item.AssignToCluster(clusterId.Value, createdAt.AddMilliseconds(1));
        }

        var leaseId = item.StartProcessing(createdAt.AddMilliseconds(2));
        item.CompleteProcessing(leaseId, createdAt.AddMilliseconds(3));

        if (deleted)
        {
            item.MarkDeleted(createdAt.AddMilliseconds(4));
        }

        feedback.Add(item);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
