using PulsePilot.Application.AI;

namespace PulsePilot.Application.Abstractions.AI;

public interface ILLMClient
{
    Task<FeedbackAnalysisResult> AnalyzeFeedbackAsync(
        FeedbackAnalysisRequest request,
        CancellationToken cancellationToken = default);
}
