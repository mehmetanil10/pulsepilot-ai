using PulsePilot.Domain.Actions;
using PulsePilot.Domain.Feedback;

namespace PulsePilot.Application.Actions;

public static class ActionRecommendationPolicy
{
    public static PendingActionType? DetermineActionType(
        FeedbackCategory category,
        FeedbackPriority priority)
    {
        if (priority is not (FeedbackPriority.P1 or FeedbackPriority.P2))
        {
            return null;
        }

        return category switch
        {
            FeedbackCategory.Bug or FeedbackCategory.FeatureRequest =>
                PendingActionType.CreateEngineeringIssue,
            FeedbackCategory.Complaint when priority == FeedbackPriority.P1 =>
                PendingActionType.EscalateIssue,
            FeedbackCategory.Complaint or FeedbackCategory.Question =>
                PendingActionType.DraftCustomerResponse,
            _ => null,
        };
    }
}
