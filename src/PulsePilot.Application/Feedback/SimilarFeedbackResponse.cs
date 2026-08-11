using PulsePilot.Domain.Feedback;

namespace PulsePilot.Application.Feedback;

public sealed record SimilarFeedbackResponse(
    Guid FeedbackId,
    double SimilarityThreshold,
    IReadOnlyList<SimilarFeedbackItemResponse> Items)
{
    public int Count => Items.Count;
}

public sealed record SimilarFeedbackItemResponse(
    Guid Id,
    string? Title,
    string Content,
    FeedbackSource Source,
    double Similarity,
    DateTimeOffset CreatedAt);
