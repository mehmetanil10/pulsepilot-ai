using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Domain.Feedback;

namespace PulsePilot.Application.FeedbackClusters;

public sealed record FeedbackClusterListResponse(
    IReadOnlyList<FeedbackClusterSummaryResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record FeedbackClusterSummaryResponse(
    Guid Id,
    string Title,
    FeedbackCategory Category,
    FeedbackComponent Component,
    int FeedbackCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    internal static FeedbackClusterSummaryResponse FromData(
        FeedbackClusterSummaryData cluster)
    {
        return new FeedbackClusterSummaryResponse(
            cluster.Id,
            cluster.Title,
            cluster.Category,
            cluster.Component,
            cluster.FeedbackCount,
            cluster.CreatedAt,
            cluster.UpdatedAt);
    }
}

public sealed record FeedbackClusterDetailResponse(
    Guid Id,
    string Title,
    FeedbackCategory Category,
    FeedbackComponent Component,
    IReadOnlyList<FeedbackClusterMemberResponse> Feedback,
    int Page,
    int PageSize,
    int TotalFeedbackCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record FeedbackClusterMemberResponse(
    Guid Id,
    string? Title,
    string Content,
    FeedbackSource Source,
    ProcessingStatus ProcessingStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    internal static FeedbackClusterMemberResponse FromData(
        FeedbackClusterMemberData feedback)
    {
        return new FeedbackClusterMemberResponse(
            feedback.Id,
            feedback.Title,
            feedback.Content,
            feedback.Source,
            feedback.ProcessingStatus,
            feedback.CreatedAt,
            feedback.UpdatedAt);
    }
}
