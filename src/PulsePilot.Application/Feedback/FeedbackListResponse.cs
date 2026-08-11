namespace PulsePilot.Application.Feedback;

public sealed record FeedbackListResponse(
    IReadOnlyList<FeedbackResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);
