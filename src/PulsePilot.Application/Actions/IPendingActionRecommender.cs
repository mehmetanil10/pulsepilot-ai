using PulsePilot.Domain.Actions;

namespace PulsePilot.Application.Actions;

public interface IPendingActionRecommender
{
    Task<PendingAction?> RecommendAsync(
        ActionRecommendationContext context,
        CancellationToken cancellationToken = default);
}
