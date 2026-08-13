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

public sealed class GetFeedbackStatisticsToolTests(PostgreSqlFixture database)
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

    private static readonly IReadOnlyDictionary<string, string?> StatisticsConfiguration =
        new Dictionary<string, string?>
        {
            ["FeedbackStatistics:DefaultPeriodDays"] = "7",
            ["FeedbackStatistics:MaxPeriodDays"] = "30",
        };

    [Fact]
    public async Task Tool_ReturnsCompleteWorkspaceScopedStatisticsForConfiguredPeriod()
    {
        var user = User.Create(
            $"statistics-tool-{Guid.CreateVersion7():N}@example.com",
            "Statistics Tool Owner",
            "password-hash",
            CurrentTime.AddDays(-10));
        var workspace = Workspace.Create(
            "Statistics Tool Workspace",
            CurrentTime.AddDays(-10));
        var otherWorkspace = Workspace.Create(
            "Other Statistics Workspace",
            CurrentTime.AddDays(-10));
        var criticalBug = CreateFeedback(
            workspace.Id,
            user.Id,
            "Payment failure",
            FeedbackSource.Manual,
            ProcessingStatus.Completed,
            CurrentTime.AddDays(-1));
        var featureRequest = CreateFeedback(
            workspace.Id,
            user.Id,
            "Dashboard filters",
            FeedbackSource.Api,
            ProcessingStatus.Completed,
            CurrentTime.AddDays(-2));
        var failed = CreateFeedback(
            workspace.Id,
            user.Id,
            "Authentication failure",
            FeedbackSource.Support,
            ProcessingStatus.Failed,
            CurrentTime.AddDays(-3));
        var pending = CreateFeedback(
            workspace.Id,
            user.Id,
            "Pending report",
            FeedbackSource.Manual,
            ProcessingStatus.Pending,
            CurrentTime.AddDays(-4));
        var processing = CreateFeedback(
            workspace.Id,
            user.Id,
            "Processing report",
            FeedbackSource.Survey,
            ProcessingStatus.Processing,
            CurrentTime.AddDays(-5));
        var inclusiveBoundary = CreateFeedback(
            workspace.Id,
            user.Id,
            "Great mobile experience",
            FeedbackSource.AppReview,
            ProcessingStatus.Completed,
            CurrentTime.AddDays(-7));
        var exclusiveBoundary = CreateFeedback(
            workspace.Id,
            user.Id,
            "New feedback at current time",
            FeedbackSource.Email,
            ProcessingStatus.Pending,
            CurrentTime);
        var outsidePeriod = CreateFeedback(
            workspace.Id,
            user.Id,
            "Old feedback",
            FeedbackSource.Email,
            ProcessingStatus.Completed,
            CurrentTime.AddDays(-7).AddTicks(-1));
        var deleted = CreateFeedback(
            workspace.Id,
            user.Id,
            "Deleted feedback",
            FeedbackSource.Email,
            ProcessingStatus.Completed,
            CurrentTime.AddDays(-1));
        deleted.MarkDeleted(CurrentTime.AddDays(-1).AddSeconds(1));
        var crossWorkspace = CreateFeedback(
            otherWorkspace.Id,
            user.Id,
            "Cross workspace feedback",
            FeedbackSource.Manual,
            ProcessingStatus.Completed,
            CurrentTime.AddDays(-1));
        var analyses = new[]
        {
            CreateAnalysis(
                criticalBug,
                FeedbackCategory.Bug,
                FeedbackComponent.Payments,
                severity: 5,
                FeedbackSentiment.Negative),
            CreateAnalysis(
                featureRequest,
                FeedbackCategory.FeatureRequest,
                FeedbackComponent.Dashboard,
                severity: 3,
                FeedbackSentiment.Neutral),
            CreateAnalysis(
                inclusiveBoundary,
                FeedbackCategory.Praise,
                FeedbackComponent.General,
                severity: 1,
                FeedbackSentiment.Positive),
            CreateAnalysis(
                pending,
                FeedbackCategory.Other,
                FeedbackComponent.Reporting,
                severity: 4,
                FeedbackSentiment.Negative),
            CreateAnalysis(
                outsidePeriod,
                FeedbackCategory.Complaint,
                FeedbackComponent.Authentication,
                severity: 4,
                FeedbackSentiment.Negative),
            CreateAnalysis(
                deleted,
                FeedbackCategory.Complaint,
                FeedbackComponent.Reporting,
                severity: 4,
                FeedbackSentiment.Negative),
            CreateAnalysis(
                crossWorkspace,
                FeedbackCategory.Bug,
                FeedbackComponent.Payments,
                severity: 5,
                FeedbackSentiment.Negative),
        };

        await using var serviceProvider = CreateServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();
        await SeedAsync(
            scope.ServiceProvider,
            user,
            [workspace, otherWorkspace],
            [
                criticalBug,
                featureRequest,
                failed,
                pending,
                processing,
                inclusiveBoundary,
                exclusiveBoundary,
                outsidePeriod,
                deleted,
                crossWorkspace,
            ],
            analyses);
        var tool = scope.ServiceProvider.GetRequiredService<IGetFeedbackStatisticsTool>();

        var result = await tool.ExecuteAsync(
            workspace.Id,
            new GetFeedbackStatisticsToolInput());
        var twoDayResult = await tool.ExecuteAsync(
            workspace.Id,
            new GetFeedbackStatisticsToolInput(PeriodDays: 2));

        Assert.Equal(CurrentTime.AddDays(-7), result.FromInclusive);
        Assert.Equal(CurrentTime, result.ToExclusive);
        Assert.Equal(7, result.PeriodDays);
        Assert.Equal(6, result.TotalFeedbackCount);
        Assert.Equal(3, result.AnalyzedFeedbackCount);
        Assert.Equal(3m, result.AverageSeverity);
        Assert.Equal(
            new Dictionary<ProcessingStatus, int>
            {
                [ProcessingStatus.Pending] = 1,
                [ProcessingStatus.Processing] = 1,
                [ProcessingStatus.Completed] = 3,
                [ProcessingStatus.Failed] = 1,
            },
            result.ProcessingStatuses.ToDictionary(item => item.Status, item => item.Count));
        Assert.Equal(
            new Dictionary<FeedbackSource, int>
            {
                [FeedbackSource.Manual] = 2,
                [FeedbackSource.Email] = 0,
                [FeedbackSource.Support] = 1,
                [FeedbackSource.Survey] = 1,
                [FeedbackSource.Api] = 1,
                [FeedbackSource.AppReview] = 1,
            },
            result.Sources.ToDictionary(item => item.Source, item => item.Count));
        Assert.Equal(1, GetCount(result.Categories, FeedbackCategory.Bug));
        Assert.Equal(1, GetCount(result.Categories, FeedbackCategory.FeatureRequest));
        Assert.Equal(1, GetCount(result.Categories, FeedbackCategory.Praise));
        Assert.Equal(0, GetCount(result.Categories, FeedbackCategory.Complaint));
        Assert.Equal(1, GetCount(result.Components, FeedbackComponent.Payments));
        Assert.Equal(1, GetCount(result.Components, FeedbackComponent.Dashboard));
        Assert.Equal(1, GetCount(result.Components, FeedbackComponent.General));
        Assert.Equal(1, GetCount(result.Sentiments, FeedbackSentiment.Positive));
        Assert.Equal(1, GetCount(result.Sentiments, FeedbackSentiment.Neutral));
        Assert.Equal(1, GetCount(result.Sentiments, FeedbackSentiment.Negative));
        Assert.Equal(
            new Dictionary<int, int>
            {
                [1] = 1,
                [2] = 0,
                [3] = 1,
                [4] = 0,
                [5] = 1,
            },
            result.Severities.ToDictionary(item => item.Severity, item => item.Count));

        Assert.Equal(CurrentTime.AddDays(-2), twoDayResult.FromInclusive);
        Assert.Equal(2, twoDayResult.TotalFeedbackCount);
        Assert.Equal(2, twoDayResult.AnalyzedFeedbackCount);
        Assert.Equal(4m, twoDayResult.AverageSeverity);
    }

    [Fact]
    public async Task Tool_ReturnsZeroFilledEmptyResultAndRejectsUntrustedBounds()
    {
        await using var serviceProvider = CreateServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();
        var tool = scope.ServiceProvider.GetRequiredService<IGetFeedbackStatisticsTool>();
        var emptyWorkspaceId = Guid.CreateVersion7();

        var result = await tool.ExecuteAsync(
            emptyWorkspaceId,
            new GetFeedbackStatisticsToolInput());

        Assert.Equal(0, result.TotalFeedbackCount);
        Assert.Equal(0, result.AnalyzedFeedbackCount);
        Assert.Null(result.AverageSeverity);
        Assert.All(result.ProcessingStatuses, item => Assert.Equal(0, item.Count));
        Assert.All(result.Sources, item => Assert.Equal(0, item.Count));
        Assert.All(result.Categories, item => Assert.Equal(0, item.Count));
        Assert.All(result.Components, item => Assert.Equal(0, item.Count));
        Assert.All(result.Sentiments, item => Assert.Equal(0, item.Count));
        Assert.All(result.Severities, item => Assert.Equal(0, item.Count));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => tool.ExecuteAsync(
            emptyWorkspaceId,
            new GetFeedbackStatisticsToolInput(PeriodDays: 0)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => tool.ExecuteAsync(
            emptyWorkspaceId,
            new GetFeedbackStatisticsToolInput(PeriodDays: 31)));
        await Assert.ThrowsAsync<ArgumentException>(() => tool.ExecuteAsync(
            Guid.Empty,
            new GetFeedbackStatisticsToolInput()));
        await Assert.ThrowsAsync<ArgumentNullException>(() => tool.ExecuteAsync(
            emptyWorkspaceId,
            null!));
    }

    private ServiceProvider CreateServiceProvider()
    {
        return database.CreateServiceProvider(
            StatisticsConfiguration,
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
        IReadOnlyList<FeedbackEntity> feedback,
        IReadOnlyList<FeedbackAnalysis> analyses)
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

        var analysisRepository = serviceProvider
            .GetRequiredService<IFeedbackAnalysisRepository>();

        foreach (var analysis in analyses)
        {
            await analysisRepository.AddAsync(analysis);
        }

        await serviceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
    }

    private static FeedbackEntity CreateFeedback(
        Guid workspaceId,
        Guid userId,
        string title,
        FeedbackSource source,
        ProcessingStatus status,
        DateTimeOffset createdAt)
    {
        var feedback = FeedbackEntity.Create(
            workspaceId,
            userId,
            title,
            $"{title} content",
            source,
            null,
            null,
            createdAt);

        if (status != ProcessingStatus.Pending)
        {
            var leaseId = feedback.StartProcessing(createdAt.AddMilliseconds(1));

            if (status == ProcessingStatus.Completed)
            {
                feedback.CompleteProcessing(leaseId, createdAt.AddMilliseconds(2));
            }
            else if (status == ProcessingStatus.Failed)
            {
                feedback.FailProcessing(leaseId, createdAt.AddMilliseconds(2));
            }
        }

        return feedback;
    }

    private static FeedbackAnalysis CreateAnalysis(
        FeedbackEntity feedback,
        FeedbackCategory category,
        FeedbackComponent component,
        int severity,
        FeedbackSentiment sentiment)
    {
        return FeedbackAnalysis.Create(
            feedback.WorkspaceId,
            feedback.Id,
            category,
            component,
            severity,
            sentiment,
            $"{feedback.Title} summary",
            "Review customer feedback.",
            0.95m,
            feedback.UpdatedAt.AddMilliseconds(1));
    }

    private static int GetCount(
        IReadOnlyList<FeedbackCategoryStatistic> statistics,
        FeedbackCategory category)
    {
        return Assert.Single(statistics, item => item.Category == category).Count;
    }

    private static int GetCount(
        IReadOnlyList<FeedbackComponentStatistic> statistics,
        FeedbackComponent component)
    {
        return Assert.Single(statistics, item => item.Component == component).Count;
    }

    private static int GetCount(
        IReadOnlyList<FeedbackSentimentStatistic> statistics,
        FeedbackSentiment sentiment)
    {
        return Assert.Single(statistics, item => item.Sentiment == sentiment).Count;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
