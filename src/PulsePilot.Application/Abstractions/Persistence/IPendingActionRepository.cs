using PulsePilot.Domain.Actions;

namespace PulsePilot.Application.Abstractions.Persistence;

public interface IPendingActionRepository
{
    Task<PendingAction?> GetByIdAsync(
        Guid workspaceId,
        Guid pendingActionId,
        CancellationToken cancellationToken = default);

    Task<PendingAction?> GetActiveByClusterAndTypeAsync(
        Guid workspaceId,
        Guid feedbackClusterId,
        PendingActionType actionType,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PendingAction>> ListAsync(
        Guid workspaceId,
        PendingActionStatus? status,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        Guid workspaceId,
        PendingActionStatus? status,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        PendingAction pendingAction,
        CancellationToken cancellationToken = default);
}
