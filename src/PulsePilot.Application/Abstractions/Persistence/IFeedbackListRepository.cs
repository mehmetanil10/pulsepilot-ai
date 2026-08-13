using PulsePilot.Domain.Feedback;

namespace PulsePilot.Application.Abstractions.Persistence;

public interface IFeedbackListRepository
{
    Task<FeedbackListPageData> GetPageAsync(
        Guid workspaceId,
        FeedbackListFilter filter,
        int skip,
        int take,
        CancellationToken cancellationToken = default);
}

public sealed record FeedbackListFilter(
    FeedbackSource? Source,
    ProcessingStatus? ProcessingStatus,
    FeedbackCategory? Category,
    FeedbackComponent? Component,
    int? Severity,
    FeedbackSentiment? Sentiment,
    DateTimeOffset? CreatedFromInclusive,
    DateTimeOffset? CreatedToExclusive,
    string? Search);

public sealed record FeedbackListPageData(
    IReadOnlyList<FeedbackListItemData> Items,
    int TotalCount);

public sealed record FeedbackListItemData(
    Guid Id,
    Guid? FeedbackClusterId,
    string? Title,
    string Content,
    FeedbackSource Source,
    ProcessingStatus ProcessingStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    FeedbackCategory? Category,
    FeedbackComponent? Component,
    int? Severity,
    FeedbackSentiment? Sentiment);
