using PulsePilot.Domain.Feedback;

namespace PulsePilot.Application.Abstractions.Persistence;

public interface IFeedbackAnalysisRepository
{
    Task<FeedbackAnalysis?> GetByFeedbackIdAsync(
        Guid workspaceId,
        Guid feedbackId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        FeedbackAnalysis analysis,
        CancellationToken cancellationToken = default);
}
