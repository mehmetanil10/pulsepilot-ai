using PulsePilot.Domain.Actions;
using PulsePilot.Domain.Backlog;

namespace PulsePilot.Application.Tools;

public interface ICreateBacklogItemTool
{
    Task<BacklogItem> ExecuteAsync(
        PendingAction pendingAction,
        Guid createdByUserId,
        DateTimeOffset executedAt,
        CancellationToken cancellationToken = default);
}
