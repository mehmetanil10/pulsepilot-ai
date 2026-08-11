using FluentValidation;
using PulsePilot.Application.Abstractions.AI;
using PulsePilot.Application.AI;
using PulsePilot.Domain.Feedback;

namespace PulsePilot.UnitTests.Application.AI;

public sealed class LlmClientContractTests
{
    private readonly FeedbackAnalysisResultValidator _validator = new();

    [Fact]
    public async Task AnalyzeFeedbackAsync_ReturnsDeterministicValidatedStructuredResult()
    {
        ILLMClient client = new DeterministicLlmClient();
        var request = new FeedbackAnalysisRequest(
            Guid.CreateVersion7(),
            "Card cannot be added",
            "After the latest update I cannot add my credit card.",
            FeedbackSource.Manual);

        var firstResult = await client.AnalyzeFeedbackAsync(request);
        var secondResult = await client.AnalyzeFeedbackAsync(request);
        var validationResult = await _validator.ValidateAsync(firstResult);

        Assert.Equal(firstResult, secondResult);
        Assert.True(validationResult.IsValid);
        Assert.Equal(FeedbackCategory.Bug, firstResult.Category);
        Assert.Equal(FeedbackComponent.Payments, firstResult.Component);
        Assert.Equal(4, firstResult.Severity);
        Assert.Equal(FeedbackSentiment.Negative, firstResult.Sentiment);
    }

    [Fact]
    public async Task Validator_RejectsUntrustedResultOutsideStructuredContract()
    {
        var result = new FeedbackAnalysisResult(
            (FeedbackCategory)999,
            FeedbackComponent.General,
            6,
            FeedbackSentiment.Neutral,
            string.Empty,
            string.Empty,
            1.5m);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            _validator.ValidateAndThrowAsync(result));

        Assert.Contains(exception.Errors, failure =>
            failure.PropertyName == nameof(FeedbackAnalysisResult.Category));
        Assert.Contains(exception.Errors, failure =>
            failure.PropertyName == nameof(FeedbackAnalysisResult.Severity));
        Assert.Contains(exception.Errors, failure =>
            failure.PropertyName == nameof(FeedbackAnalysisResult.Confidence));
    }

    private sealed class DeterministicLlmClient : ILLMClient
    {
        private static readonly FeedbackAnalysisResult PaymentFailureResult = new(
            FeedbackCategory.Bug,
            FeedbackComponent.Payments,
            4,
            FeedbackSentiment.Negative,
            "User cannot add a payment card after the latest update.",
            "Investigate the payment service and create an engineering issue.",
            0.94m);

        public Task<FeedbackAnalysisResult> AnalyzeFeedbackAsync(
            FeedbackAnalysisRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(PaymentFailureResult);
        }

        public Task<FeedbackEmbeddingResult> GenerateEmbeddingAsync(
            FeedbackEmbeddingRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(new FeedbackEmbeddingResult(
                Enumerable.Repeat(0.1f, FeedbackEmbedding.Dimensions).ToArray(),
                "deterministic-test-model"));
        }
    }
}
