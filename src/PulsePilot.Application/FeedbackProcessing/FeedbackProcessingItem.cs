using PulsePilot.Domain.Feedback;

namespace PulsePilot.Application.FeedbackProcessing;

public sealed record FeedbackProcessingItem(
    Guid FeedbackId,
    Guid WorkspaceId,
    Guid ProcessingLeaseId,
    string? Title,
    string Content,
    FeedbackSource Source);
