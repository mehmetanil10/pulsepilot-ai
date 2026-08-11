using PulsePilot.Domain.Feedback;

namespace PulsePilot.Application.Feedback;

public sealed class ListFeedbackQuery
{
    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public FeedbackSource? Source { get; init; }

    public ProcessingStatus? ProcessingStatus { get; init; }
}
