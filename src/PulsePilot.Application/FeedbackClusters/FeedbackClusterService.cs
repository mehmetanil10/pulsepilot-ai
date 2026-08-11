using PulsePilot.Application.Abstractions.Authentication;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Application.Common.Exceptions;

namespace PulsePilot.Application.FeedbackClusters;

internal sealed class FeedbackClusterService(
    IFeedbackClusterRepository clusterRepository,
    ICurrentUserContext currentUser) : IFeedbackClusterService
{
    public async Task<FeedbackClusterListResponse> ListAsync(
        FeedbackClusterQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var skip = GetSkip(query);
        var totalCount = await clusterRepository.CountAsync(
            currentUser.WorkspaceId,
            cancellationToken);
        var clusters = await clusterRepository.ListSummariesAsync(
            currentUser.WorkspaceId,
            skip,
            query.PageSize,
            cancellationToken);

        return new FeedbackClusterListResponse(
            clusters.Select(FeedbackClusterSummaryResponse.FromData).ToList(),
            query.Page,
            query.PageSize,
            totalCount);
    }

    public async Task<FeedbackClusterDetailResponse> GetByIdAsync(
        Guid clusterId,
        FeedbackClusterQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var cluster = await clusterRepository.GetByIdAsync(
            currentUser.WorkspaceId,
            clusterId,
            cancellationToken)
            ?? throw new NotFoundException("FeedbackCluster", clusterId);
        var totalFeedbackCount = await clusterRepository.CountMembersAsync(
            currentUser.WorkspaceId,
            clusterId,
            cancellationToken);

        if (totalFeedbackCount == 0)
        {
            throw new NotFoundException("FeedbackCluster", clusterId);
        }

        var feedback = await clusterRepository.ListMembersAsync(
            currentUser.WorkspaceId,
            clusterId,
            GetSkip(query),
            query.PageSize,
            cancellationToken);

        return new FeedbackClusterDetailResponse(
            cluster.Id,
            cluster.Title,
            cluster.Category,
            cluster.Component,
            feedback.Select(FeedbackClusterMemberResponse.FromData).ToList(),
            query.Page,
            query.PageSize,
            totalFeedbackCount,
            cluster.CreatedAt,
            cluster.UpdatedAt);
    }

    private static int GetSkip(FeedbackClusterQuery query)
    {
        return (int)((long)(query.Page - 1) * query.PageSize);
    }
}
