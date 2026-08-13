using PulsePilot.Domain.Feedback;

namespace PulsePilot.Application.Feedback;

public sealed class ListFeedbackQuery
{
    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public FeedbackSource? Source { get; init; }

    public ProcessingStatus? ProcessingStatus { get; init; }

    public FeedbackCategory? Category { get; init; }

    public FeedbackComponent? Component { get; init; }

    public int? Severity { get; init; }

    public FeedbackSentiment? Sentiment { get; init; }

    public DateOnly? DateFrom { get; init; }

    public DateOnly? DateTo { get; init; }

    public string? Search { get; init; }
}
