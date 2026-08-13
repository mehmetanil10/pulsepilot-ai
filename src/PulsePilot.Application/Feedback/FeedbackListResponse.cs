using PulsePilot.Domain.Feedback;

namespace PulsePilot.Application.Feedback;

public sealed record FeedbackListResponse(
    IReadOnlyList<FeedbackListItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record FeedbackListItemResponse(
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
