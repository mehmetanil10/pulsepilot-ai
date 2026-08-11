using PulsePilot.Domain.Feedback;

namespace PulsePilot.Application.Abstractions.Persistence;

public interface IFeedbackClusterRepository
{
    Task<FeedbackCluster?> GetByIdAsync(
        Guid workspaceId,
        Guid clusterId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FeedbackClusterSummaryData>> ListSummariesAsync(
        Guid workspaceId,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FeedbackClusterMemberData>> ListMembersAsync(
        Guid workspaceId,
        Guid clusterId,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<int> CountMembersAsync(
        Guid workspaceId,
        Guid clusterId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        FeedbackCluster cluster,
        CancellationToken cancellationToken = default);
}

public sealed record FeedbackClusterSummaryData(
    Guid Id,
    string Title,
    FeedbackCategory Category,
    FeedbackComponent Component,
    int FeedbackCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record FeedbackClusterMemberData(
    Guid Id,
    string? Title,
    string Content,
    FeedbackSource Source,
    ProcessingStatus ProcessingStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
