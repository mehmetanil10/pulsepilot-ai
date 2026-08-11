namespace PulsePilot.Application.AI;

public sealed record FeedbackEmbeddingRequest(
    Guid FeedbackId,
    string Input);
