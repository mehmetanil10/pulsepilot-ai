using PulsePilot.Application.AI;

namespace PulsePilot.Application.Abstractions.AI;

public interface ILLMClient
{
    Task<FeedbackAnalysisResult> AnalyzeFeedbackAsync(
        FeedbackAnalysisRequest request,
        CancellationToken cancellationToken = default);

    Task<FeedbackEmbeddingResult> GenerateEmbeddingAsync(
        FeedbackEmbeddingRequest request,
        CancellationToken cancellationToken = default);

    Task<CustomerResponseDraftResult> GenerateResponseDraftAsync(
        CustomerResponseDraftRequest request,
        CancellationToken cancellationToken = default);

    Task<ProductReportResult> GenerateReportAsync(
        ProductReportRequest request,
        CancellationToken cancellationToken = default);
}
