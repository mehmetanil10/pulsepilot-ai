namespace PulsePilot.Application.Feedback;

public interface IFeedbackService
{
    Task<FeedbackResponse> CreateAsync(
        CreateFeedbackCommand command,
        CancellationToken cancellationToken = default);

    Task<FeedbackListResponse> ListAsync(
        ListFeedbackQuery query,
        CancellationToken cancellationToken = default);

    Task<FeedbackResponse> GetByIdAsync(
        Guid feedbackId,
        CancellationToken cancellationToken = default);

    Task<FeedbackAnalysisResponse> GetAnalysisAsync(
        Guid feedbackId,
        CancellationToken cancellationToken = default);

    Task<SimilarFeedbackResponse> GetSimilarAsync(
        Guid feedbackId,
        SimilarFeedbackQuery query,
        CancellationToken cancellationToken = default);

    Task<FeedbackResponse> RetryAnalysisAsync(
        Guid feedbackId,
        CancellationToken cancellationToken = default);

    Task<FeedbackResponse> UpdateAsync(
        Guid feedbackId,
        UpdateFeedbackCommand command,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid feedbackId,
        CancellationToken cancellationToken = default);
}
