namespace PulsePilot.Application.FeedbackClusters;

public interface IFeedbackClusterService
{
    Task<FeedbackClusterListResponse> ListAsync(
        FeedbackClusterQuery query,
        CancellationToken cancellationToken = default);

    Task<FeedbackClusterDetailResponse> GetByIdAsync(
        Guid clusterId,
        FeedbackClusterQuery query,
        CancellationToken cancellationToken = default);
}
