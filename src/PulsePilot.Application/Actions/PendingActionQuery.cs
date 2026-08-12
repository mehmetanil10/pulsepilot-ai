using PulsePilot.Domain.Actions;

namespace PulsePilot.Application.Actions;

public sealed class PendingActionQuery
{
    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public PendingActionStatus? Status { get; init; }
}
