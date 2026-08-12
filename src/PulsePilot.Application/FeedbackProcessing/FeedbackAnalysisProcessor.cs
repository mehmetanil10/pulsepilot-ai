using Microsoft.Extensions.Options;
using PulsePilot.Application.Abstractions.AI;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Application.AI;
using PulsePilot.Application.Common.Exceptions;
using PulsePilot.Application.Feedback;
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
