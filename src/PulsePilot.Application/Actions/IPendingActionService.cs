namespace PulsePilot.Application.Actions;

public interface IPendingActionService
{
    Task<PendingActionListResponse> ListAsync(
        PendingActionQuery query,
        CancellationToken cancellationToken = default);

    Task<PendingActionResponse> GetByIdAsync(
        Guid pendingActionId,
        CancellationToken cancellationToken = default);
}
