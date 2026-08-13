using Microsoft.EntityFrameworkCore;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Domain.Backlog;

namespace PulsePilot.Infrastructure.Persistence.Repositories;

internal sealed class BacklogItemRepository(AppDbContext dbContext)
    : IBacklogItemRepository
{
    public Task<BacklogItem?> GetByIdAsync(
        Guid workspaceId,
        Guid backlogItemId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.BacklogItems
            .AsNoTracking()
            .SingleOrDefaultAsync(
                backlogItem => backlogItem.WorkspaceId == workspaceId
                    && backlogItem.Id == backlogItemId,
                cancellationToken);
    }

    public Task<BacklogItem?> GetBySourcePendingActionIdAsync(
        Guid workspaceId,
        Guid sourcePendingActionId,
        CancellationToken cancellationToken = default)
    {
        var trackedItem = dbContext.BacklogItems.Local.SingleOrDefault(
            backlogItem => backlogItem.WorkspaceId == workspaceId
                && backlogItem.SourcePendingActionId == sourcePendingActionId);

        if (trackedItem is not null)
        {
            return Task.FromResult<BacklogItem?>(trackedItem);
        }

        return dbContext.BacklogItems
            .AsNoTracking()
            .SingleOrDefaultAsync(
                backlogItem => backlogItem.WorkspaceId == workspaceId
                    && backlogItem.SourcePendingActionId == sourcePendingActionId,
                cancellationToken);
    }

    public Task<BacklogItem?> GetActiveBySourceClusterIdAsync(
        Guid workspaceId,
        Guid sourceClusterId,
        CancellationToken cancellationToken = default)
    {
        var trackedItem = dbContext.BacklogItems.Local.SingleOrDefault(
            backlogItem => backlogItem.WorkspaceId == workspaceId
                && backlogItem.SourceClusterId == sourceClusterId
                && (backlogItem.Status == BacklogItemStatus.Open
                    || backlogItem.Status == BacklogItemStatus.InProgress));

        if (trackedItem is not null)
        {
            return Task.FromResult<BacklogItem?>(trackedItem);
        }

        return dbContext.BacklogItems
            .AsNoTracking()
            .SingleOrDefaultAsync(
                backlogItem => backlogItem.WorkspaceId == workspaceId
                    && backlogItem.SourceClusterId == sourceClusterId
                    && (backlogItem.Status == BacklogItemStatus.Open
                        || backlogItem.Status == BacklogItemStatus.InProgress),
                cancellationToken);
    }

    public async Task<IReadOnlyList<BacklogItem>> ListAsync(
        Guid workspaceId,
        BacklogItemStatus? status,
        BacklogItemPriority? priority,
        Guid? sourcePendingActionId,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        ValidatePagination(skip, take);
        var query = CreateFilteredQuery(
            workspaceId,
            status,
            priority,
            sourcePendingActionId);

        return await query
            .OrderBy(backlogItem => backlogItem.Status == BacklogItemStatus.Open ? 0 : 1)
            .ThenBy(backlogItem => backlogItem.Priority)
            .ThenByDescending(backlogItem => backlogItem.CreatedAt)
            .ThenByDescending(backlogItem => backlogItem.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(
        Guid workspaceId,
        BacklogItemStatus? status,
        BacklogItemPriority? priority,
        Guid? sourcePendingActionId,
        CancellationToken cancellationToken = default)
    {
        return CreateFilteredQuery(
            workspaceId,
            status,
            priority,
            sourcePendingActionId)
            .CountAsync(cancellationToken);
    }

    public async Task AddAsync(
        BacklogItem backlogItem,
        CancellationToken cancellationToken = default)
    {
        await dbContext.BacklogItems.AddAsync(backlogItem, cancellationToken);
    }

    private IQueryable<BacklogItem> CreateFilteredQuery(
        Guid workspaceId,
        BacklogItemStatus? status,
        BacklogItemPriority? priority,
        Guid? sourcePendingActionId)
    {
        var query = dbContext.BacklogItems
            .AsNoTracking()
            .Where(backlogItem => backlogItem.WorkspaceId == workspaceId);

        if (status.HasValue)
        {
            query = query.Where(backlogItem => backlogItem.Status == status.Value);
        }

        if (priority.HasValue)
        {
            query = query.Where(backlogItem => backlogItem.Priority == priority.Value);
        }

        if (sourcePendingActionId.HasValue)
        {
            query = query.Where(backlogItem =>
                backlogItem.SourcePendingActionId == sourcePendingActionId.Value);
        }

        return query;
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
