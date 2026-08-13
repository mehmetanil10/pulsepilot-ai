namespace PulsePilot.Application.Backlog;

public interface IBacklogItemService
{
    Task<BacklogItemListResponse> ListAsync(
        BacklogItemQuery query,
        CancellationToken cancellationToken = default);

    Task<BacklogItemResponse> GetByIdAsync(
        Guid backlogItemId,
        CancellationToken cancellationToken = default);
}
