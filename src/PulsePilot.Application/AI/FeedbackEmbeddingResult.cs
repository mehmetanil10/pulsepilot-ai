namespace PulsePilot.Application.AI;

public sealed record FeedbackEmbeddingResult(
    IReadOnlyList<float> Values,
    string Model);
