using PulsePilot.Domain.Backlog;

namespace PulsePilot.Application.Backlog;

public sealed record BacklogItemListResponse(
    IReadOnlyList<BacklogItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record BacklogItemResponse(
    Guid Id,
    Guid SourceClusterId,
    Guid SourcePendingActionId,
    Guid CreatedByUserId,
    string Title,
    string Description,
    BacklogItemPriority Priority,
    BacklogItemStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    internal static BacklogItemResponse FromEntity(BacklogItem backlogItem)
    {
        return new BacklogItemResponse(
            backlogItem.Id,
            backlogItem.SourceClusterId,
            backlogItem.SourcePendingActionId,
            backlogItem.CreatedByUserId,
            backlogItem.Title,
            backlogItem.Description,
            backlogItem.Priority,
            backlogItem.Status,
            backlogItem.CreatedAt,
            backlogItem.UpdatedAt);
    }
}
