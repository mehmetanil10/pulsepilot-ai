using PulsePilot.Application.Abstractions.Authentication;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Application.Common.Exceptions;

namespace PulsePilot.Application.Actions;

internal sealed class PendingActionService(
    IPendingActionRepository pendingActionRepository,
    ICurrentUserContext currentUser) : IPendingActionService
{
    public async Task<PendingActionListResponse> ListAsync(
        PendingActionQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var skip = (int)((long)(query.Page - 1) * query.PageSize);
        var totalCount = await pendingActionRepository.CountAsync(
            currentUser.WorkspaceId,
            query.Status,
            cancellationToken);
        var pendingActions = await pendingActionRepository.ListAsync(
            currentUser.WorkspaceId,
            query.Status,
            skip,
            query.PageSize,
            cancellationToken);

        return new PendingActionListResponse(
            pendingActions.Select(PendingActionResponse.FromEntity).ToList(),
            query.Page,
            query.PageSize,
            totalCount);
    }

    public async Task<PendingActionResponse> GetByIdAsync(
        Guid pendingActionId,
        CancellationToken cancellationToken = default)
    {
        var pendingAction = await pendingActionRepository.GetByIdAsync(
            currentUser.WorkspaceId,
            pendingActionId,
            cancellationToken)
            ?? throw new NotFoundException("PendingAction", pendingActionId);

        return PendingActionResponse.FromEntity(pendingAction);
    }
}
