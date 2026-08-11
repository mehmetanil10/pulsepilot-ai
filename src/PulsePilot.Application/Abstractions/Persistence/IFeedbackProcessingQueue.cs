using PulsePilot.Application.FeedbackProcessing;

namespace PulsePilot.Application.Abstractions.Persistence;

public interface IFeedbackProcessingQueue
{
    Task<FeedbackProcessingItem?> ClaimNextPendingAsync(
        DateTimeOffset claimedAt,
        CancellationToken cancellationToken = default);

    Task<int> RecoverStaleAsync(
        DateTimeOffset staleBefore,
        DateTimeOffset recoveredAt,
        int maxCount,
        CancellationToken cancellationToken = default);
}
