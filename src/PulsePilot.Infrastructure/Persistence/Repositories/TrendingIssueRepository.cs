using Microsoft.EntityFrameworkCore;
using PulsePilot.Application.Abstractions.Persistence;

namespace PulsePilot.Infrastructure.Persistence.Repositories;

internal sealed class TrendingIssueRepository(AppDbContext dbContext)
    : ITrendingIssueRepository
{
    public async Task<IReadOnlyList<TrendingIssueSnapshot>> ListAsync(
        Guid workspaceId,
        DateTimeOffset previousFromInclusive,
        DateTimeOffset currentFromInclusive,
        DateTimeOffset currentToExclusive,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("Workspace id is required.", nameof(workspaceId));
        }

        if (previousFromInclusive.Offset != TimeSpan.Zero
            || currentFromInclusive.Offset != TimeSpan.Zero
            || currentToExclusive.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Trending issue boundaries must use UTC.");
        }

        if (previousFromInclusive >= currentFromInclusive
            || currentFromInclusive >= currentToExclusive)
        {
            throw new ArgumentException(
                "Trending issue boundaries must define ordered previous and current periods.");
        }

        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be positive.");
        }

        var periodCounts = dbContext.Feedback
            .AsNoTracking()
            .Where(feedback => feedback.WorkspaceId == workspaceId
                && feedback.FeedbackClusterId.HasValue
                && feedback.CreatedAt >= previousFromInclusive
                && feedback.CreatedAt < currentToExclusive)
            .GroupBy(feedback => feedback.FeedbackClusterId!.Value)
            .Select(group => new
            {
                FeedbackClusterId = group.Key,
                CurrentPeriodCount = group.Count(
                    feedback => feedback.CreatedAt >= currentFromInclusive),
                PreviousPeriodCount = group.Count(
                    feedback => feedback.CreatedAt < currentFromInclusive),
            })
            .Where(result => result.CurrentPeriodCount > result.PreviousPeriodCount);

        return await (
            from counts in periodCounts
            join cluster in dbContext.FeedbackClusters.AsNoTracking()
                .Where(cluster => cluster.WorkspaceId == workspaceId)
                on counts.FeedbackClusterId equals cluster.Id
            orderby counts.CurrentPeriodCount - counts.PreviousPeriodCount descending,
                counts.CurrentPeriodCount descending,
                cluster.PriorityScore descending,
                cluster.Id descending
            select new TrendingIssueSnapshot(
                cluster.Id,
                cluster.Title,
                cluster.Category,
                cluster.Component,
                cluster.Priority,
                cluster.PriorityScore,
                counts.CurrentPeriodCount,
                counts.PreviousPeriodCount))
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}
