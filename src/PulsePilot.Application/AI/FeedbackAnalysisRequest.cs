using PulsePilot.Domain.Feedback;

namespace PulsePilot.Application.AI;

public sealed record FeedbackAnalysisRequest(
    Guid FeedbackId,
    string? Title,
    string Content,
    FeedbackSource Source);
