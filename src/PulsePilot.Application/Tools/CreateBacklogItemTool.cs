using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Application.Common.Exceptions;
using PulsePilot.Domain.Actions;
using PulsePilot.Domain.Backlog;
using PulsePilot.Domain.Feedback;

namespace PulsePilot.Application.Tools;

internal sealed class CreateBacklogItemTool(
    IBacklogItemRepository backlogItemRepository,
    IFeedbackClusterRepository feedbackClusterRepository) : ICreateBacklogItemTool
{
    public async Task<BacklogItem> ExecuteAsync(
        PendingAction pendingAction,
        Guid createdByUserId,
        DateTimeOffset executedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pendingAction);

        if (createdByUserId == Guid.Empty)
        {
            throw new ArgumentException("Creator user id is required.", nameof(createdByUserId));
        }

        if (pendingAction.ActionType != PendingActionType.CreateEngineeringIssue)
        {
            throw new ConflictException(
                "CreateBacklogItemTool only accepts engineering issue actions.");
        }

        var existingItem = await backlogItemRepository.GetBySourcePendingActionIdAsync(
            pendingAction.WorkspaceId,
            pendingAction.Id,
            cancellationToken);

        if (existingItem is not null)
        {
            if (pendingAction.Status == PendingActionStatus.Approved)
            {
                pendingAction.MarkExecuted(executedAt);
            }
            else if (pendingAction.Status != PendingActionStatus.Executed)
            {
                throw CreateApprovalRequiredConflict();
            }

            return existingItem;
        }

        if (pendingAction.Status != PendingActionStatus.Approved)
        {
            throw CreateApprovalRequiredConflict();
        }

        var sourceCluster = await feedbackClusterRepository.GetByIdAsync(
            pendingAction.WorkspaceId,
            pendingAction.FeedbackClusterId,
            cancellationToken)
            ?? throw new NotFoundException(
                "FeedbackCluster",
                pendingAction.FeedbackClusterId);
        var activeClusterItem = await backlogItemRepository.GetActiveBySourceClusterIdAsync(
            pendingAction.WorkspaceId,
            sourceCluster.Id,
            cancellationToken);

        if (activeClusterItem is not null)
        {
            throw new ConflictException(
                "The source cluster already has an active backlog item.");
        }

        var backlogItem = BacklogItem.Create(
            pendingAction.WorkspaceId,
            sourceCluster.Id,
            pendingAction.Id,
            createdByUserId,
            pendingAction.Title,
            pendingAction.Description,
            MapPriority(sourceCluster.Priority),
            executedAt);

        await backlogItemRepository.AddAsync(backlogItem, cancellationToken);
        pendingAction.MarkExecuted(executedAt);

        return backlogItem;
    }

    private static BacklogItemPriority MapPriority(FeedbackPriority priority)
    {
        return priority switch
        {
            FeedbackPriority.P1 => BacklogItemPriority.P1,
            FeedbackPriority.P2 => BacklogItemPriority.P2,
            FeedbackPriority.P3 => BacklogItemPriority.P3,
            FeedbackPriority.P4 => BacklogItemPriority.P4,
            _ => throw new ArgumentOutOfRangeException(nameof(priority)),
        };
    }

    private static ConflictException CreateApprovalRequiredConflict()
    {
        return new ConflictException(
            "CreateBacklogItemTool requires an approved pending action.");
    }
}
