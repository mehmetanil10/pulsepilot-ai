using PulsePilot.Domain.Feedback;

using FeedbackEntity = PulsePilot.Domain.Feedback.Feedback;

namespace PulsePilot.Application.Abstractions.Persistence;

public interface IFeedbackRepository
{
    Task<FeedbackEntity?> GetByIdAsync(
        Guid workspaceId,
        Guid feedbackId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FeedbackEntity>> ListAsync(
        Guid workspaceId,
        int skip,
        int take,
        FeedbackSource? source = null,
        ProcessingStatus? processingStatus = null,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        FeedbackEntity feedback,
        CancellationToken cancellationToken = default);
}
