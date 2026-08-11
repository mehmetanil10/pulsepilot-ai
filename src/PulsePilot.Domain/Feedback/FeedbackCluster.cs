using PulsePilot.Domain.Common;

namespace PulsePilot.Domain.Feedback;

public sealed class FeedbackCluster : AuditableEntity
{
    public const int MaxTitleLength = 200;

    private FeedbackCluster()
    {
    }

    private FeedbackCluster(
        Guid id,
        Guid workspaceId,
        string title,
        FeedbackCategory category,
        FeedbackComponent component,
        DateTimeOffset createdAt)
        : base(id, createdAt)
    {
        WorkspaceId = Guard.NotEmpty(workspaceId, nameof(workspaceId));
        Title = Guard.RequiredText(title, nameof(title), MaxTitleLength);
        Category = Guard.DefinedEnum(category, nameof(category));
        Component = Guard.DefinedEnum(component, nameof(component));
    }

    public Guid WorkspaceId { get; private set; }

    public string Title { get; private set; } = null!;

    public FeedbackCategory Category { get; private set; }

    public FeedbackComponent Component { get; private set; }

    public static FeedbackCluster Create(
        Guid workspaceId,
        string title,
        FeedbackCategory category,
        FeedbackComponent component,
        DateTimeOffset createdAt)
    {
        return new FeedbackCluster(
            Guid.CreateVersion7(),
            workspaceId,
            title,
            category,
            component,
            createdAt);
    }

    public void RecordActivity(DateTimeOffset activityAt)
    {
        MarkUpdated(activityAt);
    }
}
