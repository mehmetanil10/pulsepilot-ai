using Microsoft.EntityFrameworkCore;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Domain.Feedback;

namespace PulsePilot.Infrastructure.Persistence.Repositories;

internal sealed class FeedbackStatisticsRepository(AppDbContext dbContext)
    : IFeedbackStatisticsRepository
{
    public Task<int> CountCreatedAsync(
        Guid workspaceId,
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        CancellationToken cancellationToken = default)
    {
        ValidateBoundaries(workspaceId, fromInclusive, toExclusive);

        return dbContext.Feedback
            .AsNoTracking()
            .CountAsync(
                feedback => feedback.WorkspaceId == workspaceId
                    && feedback.CreatedAt >= fromInclusive
                    && feedback.CreatedAt < toExclusive,
                cancellationToken);
    }

    public async Task<FeedbackStatisticsSnapshot> GetAsync(
        Guid workspaceId,
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        CancellationToken cancellationToken = default)
    {
        ValidateBoundaries(workspaceId, fromInclusive, toExclusive);

        var feedbackQuery = dbContext.Feedback
            .AsNoTracking()
            .Where(feedback => feedback.WorkspaceId == workspaceId
                && feedback.CreatedAt >= fromInclusive
                && feedback.CreatedAt < toExclusive);
        var completedFeedbackQuery = feedbackQuery.Where(
            feedback => feedback.ProcessingStatus == ProcessingStatus.Completed);
        var analysisQuery =
            from analysis in dbContext.FeedbackAnalyses.AsNoTracking()
            join feedback in completedFeedbackQuery
                on new { analysis.WorkspaceId, analysis.FeedbackId }
                equals new { feedback.WorkspaceId, FeedbackId = feedback.Id }
            select analysis;

        var feedbackAggregate = await feedbackQuery
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Total = group.Count(),
                Pending = group.Count(item => item.ProcessingStatus == ProcessingStatus.Pending),
                Processing = group.Count(item => item.ProcessingStatus == ProcessingStatus.Processing),
                Completed = group.Count(item => item.ProcessingStatus == ProcessingStatus.Completed),
                Failed = group.Count(item => item.ProcessingStatus == ProcessingStatus.Failed),
                Manual = group.Count(item => item.Source == FeedbackSource.Manual),
                Email = group.Count(item => item.Source == FeedbackSource.Email),
                Support = group.Count(item => item.Source == FeedbackSource.Support),
                Survey = group.Count(item => item.Source == FeedbackSource.Survey),
                Api = group.Count(item => item.Source == FeedbackSource.Api),
                AppReview = group.Count(item => item.Source == FeedbackSource.AppReview),
            })
            .SingleOrDefaultAsync(cancellationToken);
        var analysisAggregate = await analysisQuery
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Count = group.Count(),
                AverageSeverity = group.Average(analysis => (decimal)analysis.Severity),
                Bug = group.Count(item => item.Category == FeedbackCategory.Bug),
                FeatureRequest = group.Count(item => item.Category == FeedbackCategory.FeatureRequest),
                Complaint = group.Count(item => item.Category == FeedbackCategory.Complaint),
                Question = group.Count(item => item.Category == FeedbackCategory.Question),
                Praise = group.Count(item => item.Category == FeedbackCategory.Praise),
                Other = group.Count(item => item.Category == FeedbackCategory.Other),
                Payments = group.Count(item => item.Component == FeedbackComponent.Payments),
                Authentication = group.Count(item => item.Component == FeedbackComponent.Authentication),
                Dashboard = group.Count(item => item.Component == FeedbackComponent.Dashboard),
                Reporting = group.Count(item => item.Component == FeedbackComponent.Reporting),
                Mobile = group.Count(item => item.Component == FeedbackComponent.Mobile),
                Api = group.Count(item => item.Component == FeedbackComponent.Api),
                Performance = group.Count(item => item.Component == FeedbackComponent.Performance),
                General = group.Count(item => item.Component == FeedbackComponent.General),
                Positive = group.Count(item => item.Sentiment == FeedbackSentiment.Positive),
                Neutral = group.Count(item => item.Sentiment == FeedbackSentiment.Neutral),
                Negative = group.Count(item => item.Sentiment == FeedbackSentiment.Negative),
                SeverityOne = group.Count(item => item.Severity == 1),
                SeverityTwo = group.Count(item => item.Severity == 2),
                SeverityThree = group.Count(item => item.Severity == 3),
                SeverityFour = group.Count(item => item.Severity == 4),
                SeverityFive = group.Count(item => item.Severity == 5),
            })
            .SingleOrDefaultAsync(cancellationToken);

        return new FeedbackStatisticsSnapshot(
            feedbackAggregate?.Total ?? 0,
            analysisAggregate?.Count ?? 0,
            analysisAggregate?.AverageSeverity,
            feedbackAggregate is null
                ? []
                :
                [
                    new(ProcessingStatus.Pending, feedbackAggregate.Pending),
                    new(ProcessingStatus.Processing, feedbackAggregate.Processing),
                    new(ProcessingStatus.Completed, feedbackAggregate.Completed),
                    new(ProcessingStatus.Failed, feedbackAggregate.Failed),
                ],
            feedbackAggregate is null
                ? []
                :
                [
                    new(FeedbackSource.Manual, feedbackAggregate.Manual),
                    new(FeedbackSource.Email, feedbackAggregate.Email),
                    new(FeedbackSource.Support, feedbackAggregate.Support),
                    new(FeedbackSource.Survey, feedbackAggregate.Survey),
                    new(FeedbackSource.Api, feedbackAggregate.Api),
                    new(FeedbackSource.AppReview, feedbackAggregate.AppReview),
                ],
            analysisAggregate is null
                ? []
                :
                [
                    new(FeedbackCategory.Bug, analysisAggregate.Bug),
                    new(FeedbackCategory.FeatureRequest, analysisAggregate.FeatureRequest),
                    new(FeedbackCategory.Complaint, analysisAggregate.Complaint),
                    new(FeedbackCategory.Question, analysisAggregate.Question),
                    new(FeedbackCategory.Praise, analysisAggregate.Praise),
                    new(FeedbackCategory.Other, analysisAggregate.Other),
                ],
            analysisAggregate is null
                ? []
                :
                [
                    new(FeedbackComponent.Payments, analysisAggregate.Payments),
                    new(FeedbackComponent.Authentication, analysisAggregate.Authentication),
                    new(FeedbackComponent.Dashboard, analysisAggregate.Dashboard),
                    new(FeedbackComponent.Reporting, analysisAggregate.Reporting),
                    new(FeedbackComponent.Mobile, analysisAggregate.Mobile),
                    new(FeedbackComponent.Api, analysisAggregate.Api),
                    new(FeedbackComponent.Performance, analysisAggregate.Performance),
                    new(FeedbackComponent.General, analysisAggregate.General),
                ],
            analysisAggregate is null
                ? []
                :
                [
                    new(FeedbackSentiment.Positive, analysisAggregate.Positive),
                    new(FeedbackSentiment.Neutral, analysisAggregate.Neutral),
                    new(FeedbackSentiment.Negative, analysisAggregate.Negative),
                ],
            analysisAggregate is null
                ? []
                :
                [
                    new(1, analysisAggregate.SeverityOne),
                    new(2, analysisAggregate.SeverityTwo),
                    new(3, analysisAggregate.SeverityThree),
                    new(4, analysisAggregate.SeverityFour),
                    new(5, analysisAggregate.SeverityFive),
                ]);
    }

    private static void ValidateBoundaries(
        Guid workspaceId,
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive)
    {
        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("Workspace id is required.", nameof(workspaceId));
        }

        if (fromInclusive.Offset != TimeSpan.Zero || toExclusive.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Feedback statistics boundaries must use UTC.");
        }

        if (fromInclusive >= toExclusive)
        {
            throw new ArgumentException(
                "Feedback statistics start must be before the end boundary.");
        }
    }
}
