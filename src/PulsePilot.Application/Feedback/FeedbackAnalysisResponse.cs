using PulsePilot.Domain.Feedback;

using FeedbackEntity = PulsePilot.Domain.Feedback.Feedback;

namespace PulsePilot.Application.Feedback;

public sealed record FeedbackAnalysisResultResponse(
    Guid Id,
    FeedbackCategory Category,
    FeedbackComponent Component,
    int Severity,
    FeedbackSentiment Sentiment,
    string Summary,
    string SuggestedAction,
    decimal Confidence,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record FeedbackAnalysisResponse(
    Guid FeedbackId,
    ProcessingStatus ProcessingStatus,
    bool IsCurrent,
    FeedbackAnalysisResultResponse? Analysis)
{
    internal static FeedbackAnalysisResponse FromEntities(
        FeedbackEntity feedback,
        FeedbackAnalysis? analysis)
    {
        var analysisResponse = analysis is null
            ? null
            : new FeedbackAnalysisResultResponse(
                analysis.Id,
                analysis.Category,
                analysis.Component,
                analysis.Severity,
                analysis.Sentiment,
                analysis.Summary,
                analysis.SuggestedAction,
                analysis.Confidence,
                analysis.CreatedAt,
                analysis.UpdatedAt);

        return new FeedbackAnalysisResponse(
            feedback.Id,
            feedback.ProcessingStatus,
            feedback.ProcessingStatus == ProcessingStatus.Completed
                && analysisResponse is not null,
            analysisResponse);
    }
}
