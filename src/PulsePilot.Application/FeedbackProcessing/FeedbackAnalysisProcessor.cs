using Microsoft.Extensions.Options;
using PulsePilot.Application.Abstractions.AI;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Application.Actions;
using PulsePilot.Application.AI;
using PulsePilot.Application.Common.Exceptions;
using PulsePilot.Application.Feedback;
using PulsePilot.Application.Observability;
using PulsePilot.Application.Prioritization;
using PulsePilot.Domain.Feedback;

namespace PulsePilot.Application.FeedbackProcessing;

internal sealed class FeedbackAnalysisProcessor(
    IFeedbackProcessingQueue processingQueue,
    IFeedbackRepository feedbackRepository,
    IFeedbackAnalysisRepository analysisRepository,
    IFeedbackEmbeddingRepository embeddingRepository,
    IFeedbackClusterRepository clusterRepository,
    IFeedbackClusterAssignmentLock clusterAssignmentLock,
    IPriorityScoreCalculator priorityScoreCalculator,
    IPendingActionRecommender pendingActionRecommender,
    ILLMClient llmClient,
    IUnitOfWork unitOfWork,
    IOptions<FeedbackProcessingOptions> options,
    IOptions<SemanticSearchOptions> semanticSearchOptions,
    TimeProvider timeProvider) : IFeedbackAnalysisProcessor
{
    private readonly FeedbackProcessingOptions _options = options.Value;
    private readonly SemanticSearchOptions _semanticSearchOptions = semanticSearchOptions.Value;

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
        using var activity = PulsePilotTelemetry.StartFeedbackProcessing(item.Source);

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

            var result = new FeedbackAnalysisProcessResult(
                completed
                    ? FeedbackAnalysisProcessStatus.Succeeded
                    : FeedbackAnalysisProcessStatus.Abandoned,
                item.FeedbackId,
                item.WorkspaceId,
                attempts,
                timeProvider.GetElapsedTime(startedAt),
                null);
            PulsePilotTelemetry.RecordFeedbackProcessing(
                result.Status,
                result.Duration,
                result.Attempts);

            return result;
        }
        catch (AnalysisFailedException exception)
        {
            var failed = await FailAsync(item, cancellationToken);

            var result = new FeedbackAnalysisProcessResult(
                failed
                    ? FeedbackAnalysisProcessStatus.Failed
                    : FeedbackAnalysisProcessStatus.Abandoned,
                item.FeedbackId,
                item.WorkspaceId,
                exception.Attempts,
                timeProvider.GetElapsedTime(startedAt),
                exception.FailureKind);
            PulsePilotTelemetry.RecordFeedbackProcessing(
                result.Status,
                result.Duration,
                result.Attempts,
                result.FailureKind);

            return result;
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

        return await ExecuteAiOperationWithRetryAsync(
            "feedback_analysis",
            token => llmClient.AnalyzeFeedbackAsync(request, token),
            cancellationToken);
    }

    private async Task<(FeedbackEmbeddingResult Result, int Attempts)> GenerateEmbeddingWithRetryAsync(
        Guid feedbackId,
        string embeddingInput,
        CancellationToken cancellationToken)
    {
        var request = new FeedbackEmbeddingRequest(feedbackId, embeddingInput);

        return await ExecuteAiOperationWithRetryAsync(
            "embedding_generation",
            token => llmClient.GenerateEmbeddingAsync(request, token),
            cancellationToken);
    }

    private async Task<(T Result, int Attempts)> ExecuteAiOperationWithRetryAsync<T>(
        string operationName,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        using var activity = PulsePilotTelemetry.StartAiOperation(operationName);

        for (var attempt = 1; attempt <= _options.MaxAttempts; attempt++)
        {
            try
            {
                using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);
                timeoutSource.CancelAfter(TimeSpan.FromSeconds(
                    _options.AnalysisTimeoutSeconds));

                var result = await operation(timeoutSource.Token);
                PulsePilotTelemetry.RecordAiAttempt(operationName, "succeeded");
                activity?.SetTag("pulsepilot.attempts", attempt);
                activity?.SetTag("pulsepilot.outcome", "succeeded");
                activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Ok);

                return (result, attempt);
            }
            catch (LlmProviderException exception)
            {
                if (!exception.IsTransient || attempt == _options.MaxAttempts)
                {
                    PulsePilotTelemetry.RecordAiAttempt(
                        operationName,
                        "failed",
                        exception.FailureKind);
                    PulsePilotTelemetry.RecordAiOperationFailed(
                        activity,
                        attempt,
                        exception.FailureKind);
                    throw new AnalysisFailedException(
                        exception.FailureKind,
                        attempt);
                }

                PulsePilotTelemetry.RecordAiAttempt(
                    operationName,
                    "retryable_failure",
                    exception.FailureKind);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                if (attempt == _options.MaxAttempts)
                {
                    PulsePilotTelemetry.RecordAiAttempt(
                        operationName,
                        "failed",
                        LlmProviderFailureKind.ProviderUnavailable);
                    PulsePilotTelemetry.RecordAiOperationFailed(
                        activity,
                        attempt,
                        LlmProviderFailureKind.ProviderUnavailable);
                    throw new AnalysisFailedException(
                        LlmProviderFailureKind.ProviderUnavailable,
                        attempt);
                }

                PulsePilotTelemetry.RecordAiAttempt(
                    operationName,
                    "retryable_failure",
                    LlmProviderFailureKind.ProviderUnavailable);
            }
            catch (OperationCanceledException)
            {
                PulsePilotTelemetry.RecordAiAttempt(operationName, "cancelled");
                activity?.SetTag("pulsepilot.outcome", "cancelled");
                throw;
            }
            catch (Exception)
            {
                PulsePilotTelemetry.RecordAiAttempt(
                    operationName,
                    "failed",
                    LlmProviderFailureKind.ProviderFailure);
                PulsePilotTelemetry.RecordAiOperationFailed(
                    activity,
                    attempt,
                    LlmProviderFailureKind.ProviderFailure);
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
        return await clusterAssignmentLock.ExecuteAsync(
            item.WorkspaceId,
            token => CompleteUnderClusterLockAsync(
                item,
                analysisResult,
                embeddingResult,
                sourceHash,
                token),
            cancellationToken);
    }

    private async Task<bool> CompleteUnderClusterLockAsync(
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

        await AssignClusterAsync(
            feedback,
            item,
            analysisResult,
            embeddingResult,
            analyzedAt,
            cancellationToken);

        feedback.CompleteProcessing(item.ProcessingLeaseId, analyzedAt);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task AssignClusterAsync(
        PulsePilot.Domain.Feedback.Feedback feedback,
        FeedbackProcessingItem item,
        FeedbackAnalysisResult analysisResult,
        FeedbackEmbeddingResult embeddingResult,
        DateTimeOffset assignedAt,
        CancellationToken cancellationToken)
    {
        var matches = await embeddingRepository.FindSimilarByVectorAsync(
            item.WorkspaceId,
            item.FeedbackId,
            embeddingResult.Values,
            analysisResult.Category,
            analysisResult.Component,
            _semanticSearchOptions.SimilarityThreshold,
            _semanticSearchOptions.DefaultLimit,
            cancellationToken);
        var existingClusterId = matches
            .Select(match => match.FeedbackClusterId)
            .FirstOrDefault(clusterId => clusterId.HasValue);
        var cluster = existingClusterId.HasValue
            ? await clusterRepository.GetByIdAsync(
                item.WorkspaceId,
                existingClusterId.Value,
                cancellationToken)
            : null;

        if (cluster is null)
        {
            cluster = FeedbackCluster.Create(
                item.WorkspaceId,
                CreateClusterTitle(item.Title, analysisResult.Summary),
                analysisResult.Category,
                analysisResult.Component,
                assignedAt);
            await clusterRepository.AddAsync(cluster, cancellationToken);
        }

        feedback.AssignToCluster(cluster.Id, assignedAt);

        var unassignedMatchIds = matches
            .Where(match => !match.FeedbackClusterId.HasValue)
            .Select(match => match.FeedbackId)
            .ToHashSet();
        var unassignedMatches = await feedbackRepository.GetByIdsAsync(
            item.WorkspaceId,
            unassignedMatchIds,
            cancellationToken);

        foreach (var unassignedMatch in unassignedMatches)
        {
            unassignedMatch.AssignToCluster(cluster.Id, assignedAt);
        }

        var additionalFeedbackIds = unassignedMatches
            .Select(match => match.Id)
            .Append(feedback.Id)
            .ToHashSet();
        var priorityMembers = await clusterRepository.ListPriorityScoringMembersAsync(
            item.WorkspaceId,
            cluster.Id,
            additionalFeedbackIds,
            [],
            cancellationToken);
        var currentPriorityMember = new PriorityScoringMember(
            feedback.Id,
            feedback.CustomerName,
            feedback.CustomerEmail,
            feedback.CreatedAt,
            analysisResult.Severity);
        var currentMembers = priorityMembers
            .Where(member => member.FeedbackId != feedback.Id)
            .Append(currentPriorityMember)
            .GroupBy(member => member.FeedbackId)
            .Select(group => group.First())
            .ToList();
        var priority = priorityScoreCalculator.Calculate(currentMembers, assignedAt);

        cluster.RecordActivity(assignedAt);
        cluster.UpdatePriority(priority.Score, priority.Priority, assignedAt);
        await pendingActionRecommender.RecommendAsync(
            new ActionRecommendationContext(
                feedback,
                cluster,
                analysisResult,
                currentMembers.Count,
                assignedAt),
            cancellationToken);
    }

    private static string CreateClusterTitle(string? feedbackTitle, string analysisSummary)
    {
        var title = string.IsNullOrWhiteSpace(feedbackTitle)
            ? analysisSummary.Trim()
            : feedbackTitle.Trim();

        return title.Length <= FeedbackCluster.MaxTitleLength
            ? title
            : title[..FeedbackCluster.MaxTitleLength].TrimEnd();
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
