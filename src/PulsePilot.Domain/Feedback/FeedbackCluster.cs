using PulsePilot.Domain.Common;

namespace PulsePilot.Domain.Feedback;

public sealed class FeedbackCluster : AuditableEntity
{
    public const int MaxTitleLength = 200;
    public const decimal MinimumPriorityScore = 0m;
    public const decimal MaximumPriorityScore = 100m;

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
        PriorityScore = MinimumPriorityScore;
        Priority = FeedbackPriority.P4;
    }

    public Guid WorkspaceId { get; private set; }

    public string Title { get; private set; } = null!;

    public FeedbackCategory Category { get; private set; }

    public FeedbackComponent Component { get; private set; }

    public decimal PriorityScore { get; private set; }

    public FeedbackPriority Priority { get; private set; }

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

    public void UpdatePriority(
        decimal priorityScore,
        FeedbackPriority priority,
        DateTimeOffset calculatedAt)
    {
        if (priorityScore is < MinimumPriorityScore or > MaximumPriorityScore)
        {
            throw new DomainException(
                $"priorityScore must be between {MinimumPriorityScore} and {MaximumPriorityScore}.");
        }

        var validatedPriority = Guard.DefinedEnum(priority, nameof(priority));
        var roundedScore = decimal.Round(priorityScore, 2, MidpointRounding.AwayFromZero);
        MarkUpdated(calculatedAt);
        PriorityScore = roundedScore;
        Priority = validatedPriority;
    }
}
