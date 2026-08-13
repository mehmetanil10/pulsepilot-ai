using Microsoft.EntityFrameworkCore;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Domain.Feedback;

namespace PulsePilot.Infrastructure.Persistence.Repositories;

internal sealed class FeedbackStatisticsRepository(AppDbContext dbContext)
    : IFeedbackStatisticsRepository
{
    public async Task<FeedbackStatisticsSnapshot> GetAsync(
        Guid workspaceId,
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        CancellationToken cancellationToken = default)
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

        var totalFeedbackCount = await feedbackQuery.CountAsync(cancellationToken);
        var processingStatuses = await feedbackQuery
            .GroupBy(feedback => feedback.ProcessingStatus)
            .Select(group => new { Value = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var sources = await feedbackQuery
            .GroupBy(feedback => feedback.Source)
            .Select(group => new { Value = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var analysisAggregate = await analysisQuery
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Count = group.Count(),
                AverageSeverity = group.Average(analysis => (decimal)analysis.Severity),
            })
            .SingleOrDefaultAsync(cancellationToken);
        var categories = await analysisQuery
            .GroupBy(analysis => analysis.Category)
            .Select(group => new { Value = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var components = await analysisQuery
            .GroupBy(analysis => analysis.Component)
            .Select(group => new { Value = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var sentiments = await analysisQuery
            .GroupBy(analysis => analysis.Sentiment)
            .Select(group => new { Value = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var severities = await analysisQuery
            .GroupBy(analysis => analysis.Severity)
            .Select(group => new { Value = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return new FeedbackStatisticsSnapshot(
            totalFeedbackCount,
            analysisAggregate?.Count ?? 0,
            analysisAggregate?.AverageSeverity,
            processingStatuses
                .Select(item => new FeedbackStatisticCount<ProcessingStatus>(
                    item.Value,
                    item.Count))
                .ToList(),
            sources
                .Select(item => new FeedbackStatisticCount<FeedbackSource>(
                    item.Value,
                    item.Count))
                .ToList(),
            categories
                .Select(item => new FeedbackStatisticCount<FeedbackCategory>(
                    item.Value,
                    item.Count))
                .ToList(),
            components
                .Select(item => new FeedbackStatisticCount<FeedbackComponent>(
                    item.Value,
                    item.Count))
                .ToList(),
            sentiments
                .Select(item => new FeedbackStatisticCount<FeedbackSentiment>(
                    item.Value,
                    item.Count))
                .ToList(),
            severities
                .Select(item => new FeedbackSeverityCount(
                    item.Value,
                    item.Count))
                .ToList());
    }
}
