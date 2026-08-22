using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Application.Common.Exceptions;
using PulsePilot.Application.FeedbackProcessing;
using PulsePilot.Infrastructure.FeedbackProcessing;

namespace PulsePilot.Worker.UnitTests;

public sealed class FeedbackAnalysisWorkerTests
{
    [Fact]
    public async Task DisabledWorker_StopsWithoutResolvingProcessingServices()
    {
        await using var provider = new ServiceCollection().BuildServiceProvider();
        using var worker = CreateWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new FeedbackProcessingOptions { Enabled = false });

        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task EnabledWorker_RecoversStaleLeasesAndLogsSuccessfulAndFailedResults()
    {
        var feedbackId = Guid.CreateVersion7();
        var workspaceId = Guid.CreateVersion7();
        var queue = new RecordingQueue(recoveredCount: 2);
        var processor = new SequencedProcessor(
            new FeedbackAnalysisProcessResult(
                FeedbackAnalysisProcessStatus.Succeeded,
                feedbackId,
                workspaceId,
                1,
                TimeSpan.FromMilliseconds(25),
                null),
            new FeedbackAnalysisProcessResult(
                FeedbackAnalysisProcessStatus.Failed,
                feedbackId,
                workspaceId,
                3,
                TimeSpan.FromMilliseconds(75),
                LlmProviderFailureKind.ProviderUnavailable));

        var services = new ServiceCollection()
            .AddSingleton<IFeedbackProcessingQueue>(queue)
            .AddSingleton<IFeedbackAnalysisProcessor>(processor);
        await using var provider = services.BuildServiceProvider();
        var options = new FeedbackProcessingOptions
        {
            Enabled = true,
            RecoveryIntervalSeconds = 60,
            StaleProcessingThresholdMinutes = 5,
            MaxRecoveredPerSweep = 25,
        };
        using var worker = CreateWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            options);

        await worker.StartAsync(CancellationToken.None);
        await processor.WaitUntilBlockedAsync();
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(3, processor.CallCount);
        Assert.Equal(1, queue.RecoveryCallCount);
        Assert.Equal(25, queue.LastMaxCount);
        Assert.NotNull(queue.LastRecoveredAt);
        Assert.Equal(
            queue.LastRecoveredAt.Value.AddMinutes(-5),
            queue.LastStaleBefore);
    }

    private static FeedbackAnalysisWorker CreateWorker(
        IServiceScopeFactory scopeFactory,
        FeedbackProcessingOptions options)
    {
        return new FeedbackAnalysisWorker(
            scopeFactory,
            Options.Create(options),
            TimeProvider.System,
            NullLogger<FeedbackAnalysisWorker>.Instance);
    }

    private sealed class RecordingQueue(int recoveredCount) : IFeedbackProcessingQueue
    {
        public int RecoveryCallCount { get; private set; }

        public DateTimeOffset? LastStaleBefore { get; private set; }

        public DateTimeOffset? LastRecoveredAt { get; private set; }

        public int LastMaxCount { get; private set; }

        public Task<FeedbackProcessingItem?> ClaimNextPendingAsync(
            DateTimeOffset claimedAt,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<int> RecoverStaleAsync(
            DateTimeOffset staleBefore,
            DateTimeOffset recoveredAt,
            int maxCount,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RecoveryCallCount++;
            LastStaleBefore = staleBefore;
            LastRecoveredAt = recoveredAt;
            LastMaxCount = maxCount;

            return Task.FromResult(recoveredCount);
        }
    }

    private sealed class SequencedProcessor(
        params FeedbackAnalysisProcessResult[] results) : IFeedbackAnalysisProcessor
    {
        private readonly TaskCompletionSource _blocked = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public async Task<FeedbackAnalysisProcessResult> ProcessNextAsync(
            CancellationToken cancellationToken = default)
        {
            var call = Interlocked.Increment(ref _callCount);
            if (call <= results.Length)
            {
                return results[call - 1];
            }

            _blocked.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return FeedbackAnalysisProcessResult.NoWork;
        }

        public Task WaitUntilBlockedAsync()
        {
            return _blocked.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }
}
