using PulsePilot.Domain.Common;

namespace PulsePilot.Domain.Backlog;

public sealed class BacklogItem : AuditableEntity
{
    public const int MaxTitleLength = 200;
    public const int MaxDescriptionLength = 4_000;

    private BacklogItem()
    {
    }

    private BacklogItem(
        Guid id,
        Guid workspaceId,
        Guid sourceClusterId,
        Guid sourcePendingActionId,
        Guid createdByUserId,
        string title,
        string description,
        BacklogItemPriority priority,
        DateTimeOffset createdAt)
        : base(id, createdAt)
    {
        WorkspaceId = Guard.NotEmpty(workspaceId, nameof(workspaceId));
        SourceClusterId = Guard.NotEmpty(sourceClusterId, nameof(sourceClusterId));
        SourcePendingActionId = Guard.NotEmpty(
            sourcePendingActionId,
            nameof(sourcePendingActionId));
        CreatedByUserId = Guard.NotEmpty(createdByUserId, nameof(createdByUserId));
        Title = Guard.RequiredText(title, nameof(title), MaxTitleLength);
        Description = Guard.RequiredText(
            description,
            nameof(description),
            MaxDescriptionLength);
        Priority = Guard.DefinedEnum(priority, nameof(priority));
        Status = BacklogItemStatus.Open;
    }

    public Guid WorkspaceId { get; private set; }

    public Guid SourceClusterId { get; private set; }

    public Guid SourcePendingActionId { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public string Title { get; private set; } = null!;

    public string Description { get; private set; } = null!;

    public BacklogItemPriority Priority { get; private set; }

    public BacklogItemStatus Status { get; private set; }

    public static BacklogItem Create(
        Guid workspaceId,
        Guid sourceClusterId,
        Guid sourcePendingActionId,
        Guid createdByUserId,
        string title,
        string description,
        BacklogItemPriority priority,
        DateTimeOffset createdAt)
    {
        return new BacklogItem(
            Guid.CreateVersion7(),
            workspaceId,
            sourceClusterId,
            sourcePendingActionId,
            createdByUserId,
            title,
            description,
            priority,
            createdAt);
    }
}
