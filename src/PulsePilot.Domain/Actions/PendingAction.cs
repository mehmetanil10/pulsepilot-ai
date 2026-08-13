using PulsePilot.Domain.Common;

namespace PulsePilot.Domain.Actions;

public sealed class PendingAction : AuditableEntity
{
    public const int MaxTitleLength = 200;
    public const int MaxDescriptionLength = 2_000;
    public const int MaxPayloadLength = 8_000;

    private PendingAction()
    {
    }

    private PendingAction(
        Guid id,
        Guid workspaceId,
        Guid feedbackId,
        Guid feedbackClusterId,
        PendingActionType actionType,
        string title,
        string description,
        string payload,
        DateTimeOffset createdAt)
        : base(id, createdAt)
    {
        WorkspaceId = Guard.NotEmpty(workspaceId, nameof(workspaceId));
        FeedbackId = Guard.NotEmpty(feedbackId, nameof(feedbackId));
        FeedbackClusterId = Guard.NotEmpty(feedbackClusterId, nameof(feedbackClusterId));
        ActionType = Guard.DefinedEnum(actionType, nameof(actionType));
        Title = Guard.RequiredText(title, nameof(title), MaxTitleLength);
        Description = Guard.RequiredText(
            description,
            nameof(description),
            MaxDescriptionLength);
        Payload = Guard.RequiredText(payload, nameof(payload), MaxPayloadLength);
        Status = PendingActionStatus.Pending;
    }

    public Guid WorkspaceId { get; private set; }

    public Guid FeedbackId { get; private set; }

    public Guid FeedbackClusterId { get; private set; }

    public PendingActionType ActionType { get; private set; }

    public string Title { get; private set; } = null!;

    public string Description { get; private set; } = null!;

    public string Payload { get; private set; } = null!;

    public PendingActionStatus Status { get; private set; }

    public DateTimeOffset? ApprovedAt { get; private set; }

    public DateTimeOffset? RejectedAt { get; private set; }

    public DateTimeOffset? ExecutedAt { get; private set; }

    public static PendingAction Create(
        Guid workspaceId,
        Guid feedbackId,
        Guid feedbackClusterId,
        PendingActionType actionType,
        string title,
        string description,
        string payload,
        DateTimeOffset createdAt)
    {
        return new PendingAction(
            Guid.CreateVersion7(),
            workspaceId,
            feedbackId,
            feedbackClusterId,
            actionType,
            title,
            description,
            payload,
            createdAt);
    }

    public void Approve(DateTimeOffset approvedAt)
    {
        if (Status == PendingActionStatus.Approved)
        {
            return;
        }

        EnsurePending("approved");
        MarkUpdated(approvedAt);
        Status = PendingActionStatus.Approved;
        ApprovedAt = approvedAt.ToUniversalTime();
    }

    public void Reject(DateTimeOffset rejectedAt)
    {
        if (Status == PendingActionStatus.Rejected)
        {
            return;
        }

        EnsurePending("rejected");
        MarkUpdated(rejectedAt);
        Status = PendingActionStatus.Rejected;
        RejectedAt = rejectedAt.ToUniversalTime();
    }

    private void EnsurePending(string transition)
    {
        if (Status != PendingActionStatus.Pending)
        {
            throw new DomainException(
                $"Only pending actions can be {transition}.");
        }
    }
}
