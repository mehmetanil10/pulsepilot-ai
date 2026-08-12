using PulsePilot.Application.AI;
using PulsePilot.Domain.Feedback;

using FeedbackEntity = PulsePilot.Domain.Feedback.Feedback;

namespace PulsePilot.Application.Actions;

public sealed record ActionRecommendationContext(
    FeedbackEntity Feedback,
    FeedbackCluster Cluster,
    FeedbackAnalysisResult Analysis,
    int FeedbackCount,
    DateTimeOffset RecommendedAt);
