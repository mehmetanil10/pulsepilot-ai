namespace PulsePilot.Application.FeedbackProcessing;

public interface IFeedbackAnalysisProcessor
{
    Task<FeedbackAnalysisProcessResult> ProcessNextAsync(
        CancellationToken cancellationToken = default);
}
