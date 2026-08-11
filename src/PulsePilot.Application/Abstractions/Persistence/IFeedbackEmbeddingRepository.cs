using PulsePilot.Domain.Feedback;

namespace PulsePilot.Application.Abstractions.Persistence;

public interface IFeedbackEmbeddingRepository
{
    Task<FeedbackEmbedding?> GetByFeedbackIdAsync(
        Guid workspaceId,
        Guid feedbackId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SimilarFeedbackMatch>> FindSimilarAsync(
        Guid workspaceId,
        Guid feedbackId,
        double minimumSimilarity,
        int limit,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        FeedbackEmbedding embedding,
        CancellationToken cancellationToken = default);
}

public sealed record SimilarFeedbackMatch(
    Guid FeedbackId,
    string? Title,
    string Content,
    FeedbackSource Source,
    double Similarity,
    DateTimeOffset CreatedAt);
