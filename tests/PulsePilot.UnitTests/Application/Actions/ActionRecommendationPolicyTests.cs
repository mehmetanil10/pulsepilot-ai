using PulsePilot.Application.Actions;
using PulsePilot.Domain.Actions;
using PulsePilot.Domain.Feedback;

namespace PulsePilot.UnitTests.Application.Actions;

public sealed class ActionRecommendationPolicyTests
{
    [Theory]
    [InlineData(FeedbackCategory.Bug, FeedbackPriority.P1, PendingActionType.CreateEngineeringIssue)]
    [InlineData(FeedbackCategory.FeatureRequest, FeedbackPriority.P2, PendingActionType.CreateEngineeringIssue)]
    [InlineData(FeedbackCategory.Complaint, FeedbackPriority.P1, PendingActionType.EscalateIssue)]
    [InlineData(FeedbackCategory.Complaint, FeedbackPriority.P2, PendingActionType.DraftCustomerResponse)]
    [InlineData(FeedbackCategory.Question, FeedbackPriority.P2, PendingActionType.DraftCustomerResponse)]
    public void DetermineActionType_ForHighPriorityCluster_ReturnsDeterministicAction(
        FeedbackCategory category,
        FeedbackPriority priority,
        PendingActionType expectedActionType)
    {
        var actionType = ActionRecommendationPolicy.DetermineActionType(category, priority);

        Assert.Equal(expectedActionType, actionType);
    }

    [Theory]
    [InlineData(FeedbackCategory.Bug, FeedbackPriority.P3)]
    [InlineData(FeedbackCategory.Praise, FeedbackPriority.P1)]
    [InlineData(FeedbackCategory.Other, FeedbackPriority.P2)]
    public void DetermineActionType_WhenNoHumanReviewIsNeeded_ReturnsNoAction(
        FeedbackCategory category,
        FeedbackPriority priority)
    {
        var actionType = ActionRecommendationPolicy.DetermineActionType(category, priority);

        Assert.Null(actionType);
    }
}
