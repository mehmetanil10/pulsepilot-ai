using PulsePilot.Application.Abstractions.Authentication;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Application.Common.Exceptions;

namespace PulsePilot.Application.Backlog;

internal sealed class BacklogItemService(
    IBacklogItemRepository backlogItemRepository,
    ICurrentUserContext currentUser) : IBacklogItemService
{
    public async Task<BacklogItemListResponse> ListAsync(
        BacklogItemQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var skip = (int)((long)(query.Page - 1) * query.PageSize);
        var totalCount = await backlogItemRepository.CountAsync(
            currentUser.WorkspaceId,
            query.Status,
            query.Priority,
            query.SourcePendingActionId,
            cancellationToken);
        var backlogItems = await backlogItemRepository.ListAsync(
            currentUser.WorkspaceId,
            query.Status,
            query.Priority,
            query.SourcePendingActionId,
            skip,
            query.PageSize,
            cancellationToken);

        return new BacklogItemListResponse(
            backlogItems.Select(BacklogItemResponse.FromEntity).ToList(),
            query.Page,
            query.PageSize,
            totalCount);
    }

    public async Task<BacklogItemResponse> GetByIdAsync(
        Guid backlogItemId,
        CancellationToken cancellationToken = default)
    {
        var backlogItem = await backlogItemRepository.GetByIdAsync(
            currentUser.WorkspaceId,
            backlogItemId,
            cancellationToken)
            ?? throw new NotFoundException("BacklogItem", backlogItemId);

        return BacklogItemResponse.FromEntity(backlogItem);
    }
}
