using PulsePilot.Domain.Backlog;

namespace PulsePilot.Application.Backlog;

public sealed class BacklogItemQuery
{
    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public BacklogItemStatus? Status { get; init; }

    public BacklogItemPriority? Priority { get; init; }

    public Guid? SourcePendingActionId { get; init; }
}
