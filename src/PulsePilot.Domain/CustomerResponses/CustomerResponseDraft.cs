using PulsePilot.Domain.Common;

namespace PulsePilot.Domain.CustomerResponses;

public sealed class CustomerResponseDraft : AuditableEntity
{
    public const int MaxContentLength = 4_000;
    public const int MaxWordCount = 120;

    private CustomerResponseDraft()
    {
    }

    private CustomerResponseDraft(
        Guid id,
        Guid workspaceId,
        Guid feedbackId,
        Guid feedbackClusterId,
        Guid sourcePendingActionId,
        Guid createdByUserId,
        string content,
        DateTimeOffset createdAt)
        : base(id, createdAt)
    {
        WorkspaceId = Guard.NotEmpty(workspaceId, nameof(workspaceId));
        FeedbackId = Guard.NotEmpty(feedbackId, nameof(feedbackId));
        FeedbackClusterId = Guard.NotEmpty(feedbackClusterId, nameof(feedbackClusterId));
        SourcePendingActionId = Guard.NotEmpty(
            sourcePendingActionId,
            nameof(sourcePendingActionId));
        CreatedByUserId = Guard.NotEmpty(createdByUserId, nameof(createdByUserId));
        var validatedContent = Guard.RequiredText(content, nameof(content), MaxContentLength);

        if (CountWords(validatedContent) > MaxWordCount)
        {
            throw new DomainException(
                $"content cannot exceed {MaxWordCount} words.");
        }

        Content = validatedContent;
    }

    public Guid WorkspaceId { get; private set; }

    public Guid FeedbackId { get; private set; }

    public Guid FeedbackClusterId { get; private set; }

    public Guid SourcePendingActionId { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public string Content { get; private set; } = null!;

    public static CustomerResponseDraft Create(
        Guid workspaceId,
        Guid feedbackId,
        Guid feedbackClusterId,
        Guid sourcePendingActionId,
        Guid createdByUserId,
        string content,
        DateTimeOffset createdAt)
    {
        return new CustomerResponseDraft(
            Guid.CreateVersion7(),
            workspaceId,
            feedbackId,
            feedbackClusterId,
            sourcePendingActionId,
            createdByUserId,
            content,
            createdAt);
    }

    private static int CountWords(string content)
    {
        return content.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
