using Microsoft.EntityFrameworkCore;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Application.Prioritization;
using PulsePilot.Domain.Feedback;

namespace PulsePilot.Infrastructure.Persistence.Repositories;

internal sealed class FeedbackClusterRepository(AppDbContext dbContext)
    : IFeedbackClusterRepository
{
    public Task<FeedbackCluster?> GetByIdAsync(
        Guid workspaceId,
        Guid clusterId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.FeedbackClusters.SingleOrDefaultAsync(
            cluster => cluster.WorkspaceId == workspaceId && cluster.Id == clusterId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<FeedbackClusterSummaryData>> ListSummariesAsync(
        Guid workspaceId,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        ValidatePagination(skip, take);

        return await dbContext.FeedbackClusters
            .AsNoTracking()
            .Where(cluster => cluster.WorkspaceId == workspaceId
                && dbContext.Feedback.Any(feedback =>
                    feedback.WorkspaceId == workspaceId
                    && feedback.FeedbackClusterId == cluster.Id))
            .Select(cluster => new
            {
                Cluster = cluster,
                FeedbackCount = dbContext.Feedback.Count(feedback =>
                    feedback.WorkspaceId == workspaceId
                    && feedback.FeedbackClusterId == cluster.Id),
            })
            .OrderByDescending(result => result.Cluster.PriorityScore)
            .ThenBy(result => result.Cluster.Priority)
            .ThenByDescending(result => result.FeedbackCount)
            .ThenByDescending(result => result.Cluster.UpdatedAt)
            .ThenByDescending(result => result.Cluster.Id)
            .Skip(skip)
            .Take(take)
            .Select(result => new FeedbackClusterSummaryData(
                result.Cluster.Id,
                result.Cluster.Title,
                result.Cluster.Category,
                result.Cluster.Component,
                result.Cluster.PriorityScore,
                result.Cluster.Priority,
                result.FeedbackCount,
                result.Cluster.CreatedAt,
                result.Cluster.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.FeedbackClusters.CountAsync(
            cluster => cluster.WorkspaceId == workspaceId
                && dbContext.Feedback.Any(feedback =>
                    feedback.WorkspaceId == workspaceId
                    && feedback.FeedbackClusterId == cluster.Id),
            cancellationToken);
    }

    public Task<int> CountByPriorityAsync(
        Guid workspaceId,
        FeedbackPriority priority,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(priority))
        {
            throw new ArgumentOutOfRangeException(nameof(priority));
        }

        return dbContext.FeedbackClusters.CountAsync(
            cluster => cluster.WorkspaceId == workspaceId
                && cluster.Priority == priority
                && dbContext.Feedback.Any(feedback =>
                    feedback.WorkspaceId == workspaceId
                    && feedback.FeedbackClusterId == cluster.Id),
            cancellationToken);
    }

    public async Task<IReadOnlyList<FeedbackClusterMemberData>> ListMembersAsync(
        Guid workspaceId,
        Guid clusterId,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        ValidatePagination(skip, take);

        return await dbContext.Feedback
            .AsNoTracking()
            .Where(feedback => feedback.WorkspaceId == workspaceId
                && feedback.FeedbackClusterId == clusterId)
            .OrderByDescending(feedback => feedback.CreatedAt)
            .ThenByDescending(feedback => feedback.Id)
            .Skip(skip)
            .Take(take)
            .Select(feedback => new FeedbackClusterMemberData(
                feedback.Id,
                feedback.Title,
                feedback.Content,
                feedback.Source,
                feedback.ProcessingStatus,
                feedback.CreatedAt,
                feedback.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountMembersAsync(
        Guid workspaceId,
        Guid clusterId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Feedback.CountAsync(
            feedback => feedback.WorkspaceId == workspaceId
                && feedback.FeedbackClusterId == clusterId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<PriorityScoringMember>> ListPriorityScoringMembersAsync(
        Guid workspaceId,
        Guid clusterId,
        IReadOnlyCollection<Guid> additionalFeedbackIds,
        IReadOnlyCollection<Guid> excludedFeedbackIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(additionalFeedbackIds);
        ArgumentNullException.ThrowIfNull(excludedFeedbackIds);

        var additionalIds = additionalFeedbackIds.ToArray();
        var excludedIds = excludedFeedbackIds.ToArray();

        return await (
            from feedback in dbContext.Feedback.AsNoTracking()
            join analysis in dbContext.FeedbackAnalyses.AsNoTracking()
                on new { feedback.WorkspaceId, FeedbackId = feedback.Id }
                equals new { analysis.WorkspaceId, analysis.FeedbackId }
                into feedbackAnalyses
            from analysis in feedbackAnalyses.DefaultIfEmpty()
            where feedback.WorkspaceId == workspaceId
                && (feedback.FeedbackClusterId == clusterId
                    || additionalIds.Contains(feedback.Id))
                && !excludedIds.Contains(feedback.Id)
            select new PriorityScoringMember(
                feedback.Id,
                feedback.CustomerName,
                feedback.CustomerEmail,
                feedback.CreatedAt,
                analysis == null ? null : analysis.Severity))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        FeedbackCluster cluster,
        CancellationToken cancellationToken = default)
    {
        await dbContext.FeedbackClusters.AddAsync(cluster, cancellationToken);
    }

    private static void ValidatePagination(int skip, int take)
    {
        if (skip < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(skip), "Skip cannot be negative.");
        }

        if (take <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(take), "Take must be positive.");
        }
    }
}
