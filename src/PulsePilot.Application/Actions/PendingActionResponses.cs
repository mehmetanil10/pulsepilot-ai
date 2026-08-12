using System.Text.Json;
using PulsePilot.Domain.Actions;

namespace PulsePilot.Application.Actions;

public sealed record PendingActionListResponse(
    IReadOnlyList<PendingActionResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record PendingActionResponse(
    Guid Id,
    Guid FeedbackId,
    Guid FeedbackClusterId,
    PendingActionType ActionType,
    string Title,
    string Description,
    JsonElement Payload,
    PendingActionStatus Status,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset? RejectedAt,
    DateTimeOffset? ExecutedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    internal static PendingActionResponse FromEntity(PendingAction pendingAction)
    {
        using var payload = JsonDocument.Parse(pendingAction.Payload);

        return new PendingActionResponse(
            pendingAction.Id,
            pendingAction.FeedbackId,
            pendingAction.FeedbackClusterId,
            pendingAction.ActionType,
            pendingAction.Title,
            pendingAction.Description,
            payload.RootElement.Clone(),
            pendingAction.Status,
            pendingAction.ApprovedAt,
            pendingAction.RejectedAt,
            pendingAction.ExecutedAt,
            pendingAction.CreatedAt,
            pendingAction.UpdatedAt);
    }
}
