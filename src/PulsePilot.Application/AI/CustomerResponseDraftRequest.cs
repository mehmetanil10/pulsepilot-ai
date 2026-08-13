using PulsePilot.Domain.Feedback;

namespace PulsePilot.Application.AI;

public sealed record CustomerResponseDraftRequest(
    Guid FeedbackId,
    string? Title,
    string Content,
    FeedbackCategory Category,
    FeedbackComponent Component,
    int Severity,
    FeedbackSentiment Sentiment,
    string Summary);
