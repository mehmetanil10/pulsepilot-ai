using PulsePilot.Application.Abstractions.Authentication;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Application.Common.Exceptions;
using PulsePilot.Domain.Actions;
using PulsePilot.Domain.Workspaces;

namespace PulsePilot.Application.Actions;

internal sealed class PendingActionService(
    IPendingActionRepository pendingActionRepository,
    IUnitOfWork unitOfWork,
    ICurrentUserContext currentUser,
    TimeProvider timeProvider) : IPendingActionService
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

    public Task<PendingActionResponse> ApproveAsync(
        Guid pendingActionId,
        CancellationToken cancellationToken = default)
    {
        return ReviewAsync(
            pendingActionId,
            PendingActionStatus.Approved,
            cancellationToken);
    }

    public Task<PendingActionResponse> RejectAsync(
        Guid pendingActionId,
        CancellationToken cancellationToken = default)
    {
        return ReviewAsync(
            pendingActionId,
            PendingActionStatus.Rejected,
            cancellationToken);
    }

    private async Task<PendingActionResponse> ReviewAsync(
        Guid pendingActionId,
        PendingActionStatus decision,
        CancellationToken cancellationToken)
    {
        EnsureAdminReviewer();

        var pendingAction = await pendingActionRepository.GetForUpdateAsync(
            currentUser.WorkspaceId,
            pendingActionId,
            cancellationToken)
            ?? throw new NotFoundException("PendingAction", pendingActionId);

        if (pendingAction.Status == decision)
        {
            return PendingActionResponse.FromEntity(pendingAction);
        }

        if (pendingAction.Status != PendingActionStatus.Pending)
        {
            throw CreateAlreadyReviewedConflict();
        }

        var reviewedAt = timeProvider.GetUtcNow();

        if (decision == PendingActionStatus.Approved)
        {
            pendingAction.Approve(reviewedAt);
        }
        else
        {
            pendingAction.Reject(reviewedAt);
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            var currentAction = await pendingActionRepository.GetByIdAsync(
                currentUser.WorkspaceId,
                pendingActionId,
                cancellationToken);

            if (currentAction?.Status == decision)
            {
                return PendingActionResponse.FromEntity(currentAction);
            }

            throw CreateAlreadyReviewedConflict();
        }

        var persistedAction = await pendingActionRepository.GetByIdAsync(
            currentUser.WorkspaceId,
            pendingActionId,
            cancellationToken)
            ?? throw new NotFoundException("PendingAction", pendingActionId);

        return PendingActionResponse.FromEntity(persistedAction);
    }

    private void EnsureAdminReviewer()
    {
        if (!string.Equals(
            currentUser.Role,
            WorkspaceRole.Admin.ToString(),
            StringComparison.Ordinal))
        {
            throw new ForbiddenException(
                "Only workspace admins can approve or reject pending actions.");
        }
    }

    private static ConflictException CreateAlreadyReviewedConflict()
    {
        return new ConflictException(
            "The pending action has already received a different or terminal decision.");
    }
}
