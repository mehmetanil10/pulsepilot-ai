using PulsePilot.Domain.Feedback;

using FeedbackEntity = PulsePilot.Domain.Feedback.Feedback;

namespace PulsePilot.Application.Feedback;

public sealed record FeedbackResponse(
    Guid Id,
    Guid WorkspaceId,
    Guid? FeedbackClusterId,
    Guid CreatedByUserId,
    string? Title,
    string Content,
    FeedbackSource Source,
    string? CustomerName,
    string? CustomerEmail,
    ProcessingStatus ProcessingStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    internal static FeedbackResponse FromEntity(FeedbackEntity feedback)
    {
        return new FeedbackResponse(
            feedback.Id,
            feedback.WorkspaceId,
            feedback.FeedbackClusterId,
            feedback.CreatedByUserId,
            feedback.Title,
            feedback.Content,
            feedback.Source,
            feedback.CustomerName,
            feedback.CustomerEmail,
            feedback.ProcessingStatus,
            feedback.CreatedAt,
            feedback.UpdatedAt);
    }
}
