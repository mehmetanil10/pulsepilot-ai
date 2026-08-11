using Microsoft.Extensions.Options;
using PulsePilot.Application.Abstractions.AI;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Application.AI;
using PulsePilot.Application.Common.Exceptions;
using PulsePilot.Domain.Feedback;

namespace PulsePilot.Application.FeedbackProcessing;

internal sealed class FeedbackAnalysisProcessor(
    IFeedbackProcessingQueue processingQueue,
    IFeedbackRepository feedbackRepository,
    IFeedbackAnalysisRepository analysisRepository,
    IFeedbackEmbeddingRepository embeddingRepository,
    ILLMClient llmClient,
    IUnitOfWork unitOfWork,
    IOptions<FeedbackProcessingOptions> options,
    TimeProvider timeProvider) : IFeedbackAnalysisProcessor
{
    private readonly FeedbackProcessingOptions _options = options.Value;

    public async Task<FeedbackAnalysisProcessResult> ProcessNextAsync(
        CancellationToken cancellationToken = default)
    {
        var item = await processingQueue.ClaimNextPendingAsync(
            timeProvider.GetUtcNow(),
            cancellationToken);

        if (item is null)
        {
            return FeedbackAnalysisProcessResult.NoWork;
        }

        var startedAt = timeProvider.GetTimestamp();

        try
        {
            var embeddingInput = FeedbackEmbeddingSource.CreateText(
                item.Title,
                item.Content);
            var sourceHash = FeedbackEmbeddingSource.ComputeHash(embeddingInput);
            var (analysisResult, analysisAttempts) = await AnalyzeWithRetryAsync(
                item,
                cancellationToken);
            var (embeddingResult, embeddingAttempts) = await GenerateEmbeddingWithRetryAsync(
                item.FeedbackId,
                embeddingInput,
                cancellationToken);
            var attempts = Math.Max(analysisAttempts, embeddingAttempts);
            var completed = await CompleteAsync(
                item,
                analysisResult,
                embeddingResult,
                sourceHash,
                cancellationToken);

            return new FeedbackAnalysisProcessResult(
                completed
                    ? FeedbackAnalysisProcessStatus.Succeeded
                    : FeedbackAnalysisProcessStatus.Abandoned,
                item.FeedbackId,
                item.WorkspaceId,
                attempts,
                timeProvider.GetElapsedTime(startedAt),
                null);
        }
        catch (AnalysisFailedException exception)
        {
            var failed = await FailAsync(item, cancellationToken);

            return new FeedbackAnalysisProcessResult(
                failed
                    ? FeedbackAnalysisProcessStatus.Failed
                    : FeedbackAnalysisProcessStatus.Abandoned,
                item.FeedbackId,
                item.WorkspaceId,
                exception.Attempts,
                timeProvider.GetElapsedTime(startedAt),
                exception.FailureKind);
        }
    }

    private async Task<(FeedbackAnalysisResult Result, int Attempts)> AnalyzeWithRetryAsync(
        FeedbackProcessingItem item,
        CancellationToken cancellationToken)
    {
        var request = new FeedbackAnalysisRequest(
            item.FeedbackId,
            item.Title,
            item.Content,
            item.Source);

        return await ExecuteWithRetryAsync(
            token => llmClient.AnalyzeFeedbackAsync(request, token),
            cancellationToken);
    }

    private async Task<(FeedbackEmbeddingResult Result, int Attempts)> GenerateEmbeddingWithRetryAsync(
        Guid feedbackId,
        string embeddingInput,
        CancellationToken cancellationToken)
    {
        var request = new FeedbackEmbeddingRequest(feedbackId, embeddingInput);

        return await ExecuteWithRetryAsync(
            token => llmClient.GenerateEmbeddingAsync(request, token),
            cancellationToken);
    }

    private async Task<(T Result, int Attempts)> ExecuteWithRetryAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        for (var attempt = 1; attempt <= _options.MaxAttempts; attempt++)
        {
            try
            {
                using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);
                timeoutSource.CancelAfter(TimeSpan.FromSeconds(
                    _options.AnalysisTimeoutSeconds));

                var result = await operation(timeoutSource.Token);

                return (result, attempt);
            }
            catch (LlmProviderException exception)
            {
                if (!exception.IsTransient || attempt == _options.MaxAttempts)
                {
                    throw new AnalysisFailedException(
                        exception.FailureKind,
                        attempt);
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                if (attempt == _options.MaxAttempts)
                {
                    throw new AnalysisFailedException(
                        LlmProviderFailureKind.ProviderUnavailable,
                        attempt);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                throw new AnalysisFailedException(
                    LlmProviderFailureKind.ProviderFailure,
                    attempt);
            }

            await DelayBeforeRetryAsync(attempt, cancellationToken);
        }

        throw new InvalidOperationException("Feedback analysis retry loop completed unexpectedly.");
    }

    private async Task<bool> CompleteAsync(
        FeedbackProcessingItem item,
        FeedbackAnalysisResult analysisResult,
        FeedbackEmbeddingResult embeddingResult,
        string sourceHash,
        CancellationToken cancellationToken)
    {
        var feedback = await feedbackRepository.GetByIdAsync(
            item.WorkspaceId,
            item.FeedbackId,
            cancellationToken);

        if (feedback is null
            || !feedback.HasActiveProcessingLease(item.ProcessingLeaseId))
        {
            return false;
        }

        var analyzedAt = timeProvider.GetUtcNow();
        var analysis = await analysisRepository.GetByFeedbackIdAsync(
            item.WorkspaceId,
            item.FeedbackId,
            cancellationToken);

        if (analysis is null)
        {
            analysis = FeedbackAnalysis.Create(
                item.WorkspaceId,
                item.FeedbackId,
                analysisResult.Category,
                analysisResult.Component,
                analysisResult.Severity,
                analysisResult.Sentiment,
                analysisResult.Summary,
                analysisResult.SuggestedAction,
                analysisResult.Confidence,
                analyzedAt);
            await analysisRepository.AddAsync(analysis, cancellationToken);
        }
        else
        {
            analysis.ReplaceResult(
                analysisResult.Category,
                analysisResult.Component,
                analysisResult.Severity,
                analysisResult.Sentiment,
                analysisResult.Summary,
                analysisResult.SuggestedAction,
                analysisResult.Confidence,
                analyzedAt);
        }

        var embedding = await embeddingRepository.GetByFeedbackIdAsync(
            item.WorkspaceId,
            item.FeedbackId,
            cancellationToken);

        if (embedding is null)
        {
            embedding = FeedbackEmbedding.Create(
                item.WorkspaceId,
                item.FeedbackId,
                embeddingResult.Values,
                embeddingResult.Model,
                sourceHash,
                analyzedAt);
            await embeddingRepository.AddAsync(embedding, cancellationToken);
        }
        else
        {
            embedding.ReplaceResult(
                embeddingResult.Values,
                embeddingResult.Model,
                sourceHash,
                analyzedAt);
        }

        feedback.CompleteProcessing(item.ProcessingLeaseId, analyzedAt);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task<bool> FailAsync(
        FeedbackProcessingItem item,
        CancellationToken cancellationToken)
    {
        var feedback = await feedbackRepository.GetByIdAsync(
            item.WorkspaceId,
            item.FeedbackId,
            cancellationToken);

        if (feedback is null
            || !feedback.HasActiveProcessingLease(item.ProcessingLeaseId))
        {
            return false;
        }

        feedback.FailProcessing(item.ProcessingLeaseId, timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    private Task DelayBeforeRetryAsync(
        int failedAttempt,
        CancellationToken cancellationToken)
    {
        var baseDelayMilliseconds = _options.BaseRetryDelayMilliseconds
            * Math.Pow(2, failedAttempt - 1);
        var jitterMultiplier = 1
            - _options.RetryJitterFactor
            + 2 * _options.RetryJitterFactor * Random.Shared.NextDouble();
        var maxDelayMilliseconds = TimeSpan
            .FromSeconds(_options.MaxRetryDelaySeconds)
            .TotalMilliseconds;
        var delay = TimeSpan.FromMilliseconds(Math.Min(
            baseDelayMilliseconds * jitterMultiplier,
            maxDelayMilliseconds));

        return Task.Delay(delay, timeProvider, cancellationToken);
    }

    private sealed class AnalysisFailedException(
        LlmProviderFailureKind failureKind,
        int attempts) : Exception
    {
        public LlmProviderFailureKind FailureKind { get; } = failureKind;

        public int Attempts { get; } = attempts;
    }
}
