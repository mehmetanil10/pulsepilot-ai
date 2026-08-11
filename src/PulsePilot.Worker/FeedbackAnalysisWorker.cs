using Microsoft.Extensions.Options;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Application.FeedbackProcessing;

namespace PulsePilot.Worker;

public sealed class FeedbackAnalysisWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<FeedbackProcessingOptions> options,
    TimeProvider timeProvider,
    ILogger<FeedbackAnalysisWorker> logger) : BackgroundService
{
    private readonly FeedbackProcessingOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Feedback analysis worker is disabled");
            return;
        }

        logger.LogInformation(
            "Feedback analysis worker started with {MaxAttempts} maximum attempts",
            _options.MaxAttempts);

        var nextRecoveryAt = DateTimeOffset.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = timeProvider.GetUtcNow();

                if (now >= nextRecoveryAt)
                {
                    await RecoverStaleProcessingAsync(now, stoppingToken);
                    nextRecoveryAt = now.AddSeconds(_options.RecoveryIntervalSeconds);
                }

                var result = await ProcessNextAsync(stoppingToken);

                if (result.Status == FeedbackAnalysisProcessStatus.NoWork)
                {
                    await Task.Delay(
                        TimeSpan.FromMilliseconds(_options.PollIntervalMilliseconds),
                        timeProvider,
                        stoppingToken);
                    continue;
                }

                LogResult(result);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    "Feedback analysis worker iteration failed with {ErrorType}",
                    exception.GetType().Name);
                await Task.Delay(
                    TimeSpan.FromMilliseconds(_options.PollIntervalMilliseconds),
                    timeProvider,
                    stoppingToken);
            }
        }

        logger.LogInformation("Feedback analysis worker stopped");
    }

    private async Task<FeedbackAnalysisProcessResult> ProcessNextAsync(
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var processor = scope.ServiceProvider
            .GetRequiredService<IFeedbackAnalysisProcessor>();

        return await processor.ProcessNextAsync(cancellationToken);
    }

    private async Task RecoverStaleProcessingAsync(
        DateTimeOffset recoveredAt,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var queue = scope.ServiceProvider.GetRequiredService<IFeedbackProcessingQueue>();
        var recoveredCount = await queue.RecoverStaleAsync(
            recoveredAt.AddMinutes(-_options.StaleProcessingThresholdMinutes),
            recoveredAt,
            _options.MaxRecoveredPerSweep,
            cancellationToken);

        if (recoveredCount > 0)
        {
            logger.LogWarning(
                "Recovered {RecoveredCount} stale feedback processing leases",
                recoveredCount);
        }
    }

    private void LogResult(FeedbackAnalysisProcessResult result)
    {
        if (result.Status == FeedbackAnalysisProcessStatus.Succeeded)
        {
            logger.LogInformation(
                "Feedback analysis completed for {FeedbackId} in workspace {WorkspaceId} after {Attempts} attempt(s) in {DurationMilliseconds} ms",
                result.FeedbackId,
                result.WorkspaceId,
                result.Attempts,
                result.Duration.TotalMilliseconds);
            return;
        }

        logger.LogWarning(
            "Feedback analysis ended with {ProcessingStatus} for {FeedbackId} in workspace {WorkspaceId} after {Attempts} attempt(s); failure kind {FailureKind}",
            result.Status,
            result.FeedbackId,
            result.WorkspaceId,
            result.Attempts,
            result.FailureKind);
    }
}
