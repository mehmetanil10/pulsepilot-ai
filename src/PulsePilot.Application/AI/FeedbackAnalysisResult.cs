using PulsePilot.Domain.Feedback;

namespace PulsePilot.Application.AI;

public sealed record FeedbackAnalysisResult(
    FeedbackCategory Category,
    FeedbackComponent Component,
    int Severity,
    FeedbackSentiment Sentiment,
    string Summary,
    string SuggestedAction,
    decimal Confidence);
