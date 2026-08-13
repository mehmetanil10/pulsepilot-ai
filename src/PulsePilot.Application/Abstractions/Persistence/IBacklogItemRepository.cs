using PulsePilot.Domain.Backlog;

namespace PulsePilot.Application.Abstractions.Persistence;

public interface IBacklogItemRepository
{
    Task<BacklogItem?> GetByIdAsync(
        Guid workspaceId,
        Guid backlogItemId,
        CancellationToken cancellationToken = default);

    Task<BacklogItem?> GetBySourcePendingActionIdAsync(
        Guid workspaceId,
        Guid sourcePendingActionId,
        CancellationToken cancellationToken = default);

    Task<BacklogItem?> GetActiveBySourceClusterIdAsync(
        Guid workspaceId,
        Guid sourceClusterId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BacklogItem>> ListAsync(
        Guid workspaceId,
        BacklogItemStatus? status,
        BacklogItemPriority? priority,
        Guid? sourcePendingActionId,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        Guid workspaceId,
        BacklogItemStatus? status,
        BacklogItemPriority? priority,
        Guid? sourcePendingActionId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        BacklogItem backlogItem,
        CancellationToken cancellationToken = default);
}
