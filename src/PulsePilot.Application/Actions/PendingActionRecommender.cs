using System.Text.Json;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Domain.Actions;
using PulsePilot.Domain.Feedback;

namespace PulsePilot.Application.Actions;

internal sealed class PendingActionRecommender(
    IPendingActionRepository pendingActionRepository) : IPendingActionRecommender
{
    private static readonly JsonSerializerOptions SerializerOptions = new(
        JsonSerializerDefaults.Web);

    public async Task<PendingAction?> RecommendAsync(
        ActionRecommendationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.FeedbackCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(context),
                "Feedback count must be greater than zero.");
        }

        var actionType = ActionRecommendationPolicy.DetermineActionType(
            context.Cluster.Category,
            context.Cluster.Priority);

        if (!actionType.HasValue)
        {
            return null;
        }

        var existingAction = await pendingActionRepository.GetActiveByClusterAndTypeAsync(
            context.Cluster.WorkspaceId,
            context.Cluster.Id,
            actionType.Value,
            cancellationToken);

        if (existingAction is not null)
        {
            return existingAction;
        }

        var pendingAction = PendingAction.Create(
            context.Cluster.WorkspaceId,
            context.Feedback.Id,
            context.Cluster.Id,
            actionType.Value,
            CreateTitle(context.Cluster),
            CreateDescription(actionType.Value, context.Cluster, context.FeedbackCount),
            JsonSerializer.Serialize(
                new ActionRecommendationPayload(
                    context.Feedback.Id,
                    context.Cluster.Id,
                    context.Cluster.Priority,
                    context.Cluster.PriorityScore,
                    context.Cluster.Category,
                    context.Cluster.Component,
                    context.FeedbackCount,
                    context.Analysis.SuggestedAction),
                SerializerOptions),
            context.RecommendedAt);
        await pendingActionRepository.AddAsync(pendingAction, cancellationToken);

        return pendingAction;
    }

    private static string CreateTitle(FeedbackCluster cluster)
    {
        var title = $"[{cluster.Priority}] {cluster.Title}";

        return title.Length <= PendingAction.MaxTitleLength
            ? title
            : title[..PendingAction.MaxTitleLength].TrimEnd();
    }

    private static string CreateDescription(
        PendingActionType actionType,
        FeedbackCluster cluster,
        int feedbackCount)
    {
        var action = actionType switch
        {
            PendingActionType.CreateEngineeringIssue => "Create an engineering issue",
            PendingActionType.DraftCustomerResponse => "Draft a customer response",
            PendingActionType.EscalateIssue => "Escalate the issue",
            PendingActionType.GenerateReport => "Generate a report",
            _ => throw new ArgumentOutOfRangeException(nameof(actionType)),
        };

        return $"{action} for the {cluster.Priority} cluster '{cluster.Title}' "
            + $"because it contains {feedbackCount} related feedback report(s).";
    }

    private sealed record ActionRecommendationPayload(
        Guid FeedbackId,
        Guid FeedbackClusterId,
        FeedbackPriority Priority,
        decimal PriorityScore,
        FeedbackCategory Category,
        FeedbackComponent Component,
        int FeedbackCount,
        string SuggestedAction);
}
