using Microsoft.EntityFrameworkCore;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Domain.Actions;

namespace PulsePilot.Infrastructure.Persistence.Repositories;

internal sealed class PendingActionRepository(AppDbContext dbContext)
    : IPendingActionRepository
{
    public Task<PendingAction?> GetByIdAsync(
        Guid workspaceId,
        Guid pendingActionId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.PendingActions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                pendingAction => pendingAction.WorkspaceId == workspaceId
                    && pendingAction.Id == pendingActionId,
                cancellationToken);
    }

    public Task<PendingAction?> GetActiveByClusterAndTypeAsync(
        Guid workspaceId,
        Guid feedbackClusterId,
        PendingActionType actionType,
        CancellationToken cancellationToken = default)
    {
        return dbContext.PendingActions.SingleOrDefaultAsync(
            pendingAction => pendingAction.WorkspaceId == workspaceId
                && pendingAction.FeedbackClusterId == feedbackClusterId
                && pendingAction.ActionType == actionType
                && (pendingAction.Status == PendingActionStatus.Pending
                    || pendingAction.Status == PendingActionStatus.Approved),
            cancellationToken);
    }

    public async Task<IReadOnlyList<PendingAction>> ListAsync(
        Guid workspaceId,
        PendingActionStatus? status,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        ValidatePagination(skip, take);

        var query = dbContext.PendingActions
            .AsNoTracking()
            .Where(pendingAction => pendingAction.WorkspaceId == workspaceId);

        if (status.HasValue)
        {
            query = query.Where(pendingAction => pendingAction.Status == status.Value);
        }

        return await query
            .OrderBy(pendingAction => pendingAction.Status == PendingActionStatus.Pending ? 0 : 1)
            .ThenByDescending(pendingAction => pendingAction.CreatedAt)
            .ThenByDescending(pendingAction => pendingAction.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(
        Guid workspaceId,
        PendingActionStatus? status,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.PendingActions
            .Where(pendingAction => pendingAction.WorkspaceId == workspaceId);

        if (status.HasValue)
        {
            query = query.Where(pendingAction => pendingAction.Status == status.Value);
        }

        return query.CountAsync(cancellationToken);
    }

    public async Task AddAsync(
        PendingAction pendingAction,
        CancellationToken cancellationToken = default)
    {
        await dbContext.PendingActions.AddAsync(pendingAction, cancellationToken);
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
