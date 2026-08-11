using PulsePilot.Domain.Common;

namespace PulsePilot.Domain.Feedback;

public sealed class Feedback : AuditableEntity
{
    public const int MaxTitleLength = 200;
    public const int MaxContentLength = 10_000;
    public const int MaxCustomerNameLength = 150;
    public const int MaxCustomerEmailLength = 320;

    private Feedback()
    {
    }

    private Feedback(
        Guid id,
        Guid workspaceId,
        Guid createdByUserId,
        string? title,
        string content,
        FeedbackSource source,
        string? customerName,
        string? customerEmail,
        DateTimeOffset createdAt)
        : base(id, createdAt)
    {
        WorkspaceId = Guard.NotEmpty(workspaceId, nameof(workspaceId));
        CreatedByUserId = Guard.NotEmpty(createdByUserId, nameof(createdByUserId));
        Title = Guard.OptionalText(title, nameof(title), MaxTitleLength);
        Content = Guard.RequiredText(content, nameof(content), MaxContentLength);
        Source = Guard.DefinedEnum(source, nameof(source));
        CustomerName = Guard.OptionalText(customerName, nameof(customerName), MaxCustomerNameLength);
        CustomerEmail = Guard.OptionalEmail(customerEmail, nameof(customerEmail), MaxCustomerEmailLength);
        ProcessingStatus = ProcessingStatus.Pending;
    }

    public Guid WorkspaceId { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public string? Title { get; private set; }

    public string Content { get; private set; } = null!;

    public FeedbackSource Source { get; private set; }

    public string? CustomerName { get; private set; }

    public string? CustomerEmail { get; private set; }

    public ProcessingStatus ProcessingStatus { get; private set; }

    public Guid? ProcessingLeaseId { get; private set; }

    public DateTimeOffset? ProcessingStartedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public bool IsDeleted => DeletedAt.HasValue;

    public static Feedback Create(
        Guid workspaceId,
        Guid createdByUserId,
        string? title,
        string content,
        FeedbackSource source,
        string? customerName,
        string? customerEmail,
        DateTimeOffset createdAt)
    {
        return new Feedback(
            Guid.CreateVersion7(),
            workspaceId,
            createdByUserId,
            title,
            content,
            source,
            customerName,
            customerEmail,
            createdAt);
    }

    public void UpdateDetails(
        string? title,
        string content,
        FeedbackSource source,
        string? customerName,
        string? customerEmail,
        DateTimeOffset updatedAt)
    {
        EnsureNotDeleted();

        if (ProcessingStatus == ProcessingStatus.Processing)
        {
            throw new DomainException("Feedback cannot be updated while it is being processed.");
        }

        var validatedTitle = Guard.OptionalText(title, nameof(title), MaxTitleLength);
        var validatedContent = Guard.RequiredText(content, nameof(content), MaxContentLength);
        var validatedSource = Guard.DefinedEnum(source, nameof(source));
        var validatedCustomerName = Guard.OptionalText(
            customerName,
            nameof(customerName),
            MaxCustomerNameLength);
        var validatedCustomerEmail = Guard.OptionalEmail(
            customerEmail,
            nameof(customerEmail),
            MaxCustomerEmailLength);

        MarkUpdated(updatedAt);
        Title = validatedTitle;
        Content = validatedContent;
        Source = validatedSource;
        CustomerName = validatedCustomerName;
        CustomerEmail = validatedCustomerEmail;
        ProcessingStatus = ProcessingStatus.Pending;
        ClearProcessingLease();
    }

    public Guid StartProcessing(DateTimeOffset updatedAt)
    {
        EnsureNotDeleted();
        ChangeProcessingStatus(ProcessingStatus.Pending, ProcessingStatus.Processing, updatedAt);

        ProcessingLeaseId = Guid.CreateVersion7();
        ProcessingStartedAt = UpdatedAt;

        return ProcessingLeaseId.Value;
    }

    public void CompleteProcessing(DateTimeOffset updatedAt)
    {
        CompleteProcessing(GetRequiredProcessingLease(), updatedAt);
    }

    public void CompleteProcessing(Guid processingLeaseId, DateTimeOffset updatedAt)
    {
        EnsureActiveProcessingLease(processingLeaseId);
        ChangeProcessingStatus(ProcessingStatus.Processing, ProcessingStatus.Completed, updatedAt);
        ClearProcessingLease();
    }

    public void FailProcessing(DateTimeOffset updatedAt)
    {
        FailProcessing(GetRequiredProcessingLease(), updatedAt);
    }

    public void FailProcessing(Guid processingLeaseId, DateTimeOffset updatedAt)
    {
        EnsureActiveProcessingLease(processingLeaseId);
        ChangeProcessingStatus(ProcessingStatus.Processing, ProcessingStatus.Failed, updatedAt);
        ClearProcessingLease();
    }

    public void RetryProcessing(DateTimeOffset updatedAt)
    {
        EnsureNotDeleted();
        ChangeProcessingStatus(ProcessingStatus.Failed, ProcessingStatus.Pending, updatedAt);
        ClearProcessingLease();
    }

    public bool HasActiveProcessingLease(Guid processingLeaseId)
    {
        return ProcessingStatus == ProcessingStatus.Processing
            && processingLeaseId != Guid.Empty
            && ProcessingLeaseId == processingLeaseId;
    }

    public void MarkDeleted(DateTimeOffset deletedAt)
    {
        if (IsDeleted)
        {
            return;
        }

        var timestamp = Guard.UtcTimestamp(deletedAt, nameof(deletedAt));
        MarkUpdated(timestamp);
        DeletedAt = timestamp;
    }

    private void ChangeProcessingStatus(
        ProcessingStatus expectedStatus,
        ProcessingStatus nextStatus,
        DateTimeOffset updatedAt)
    {
        if (ProcessingStatus != expectedStatus)
        {
            throw new DomainException(
                $"Feedback must be {expectedStatus} before it can transition to {nextStatus}.");
        }

        MarkUpdated(updatedAt);
        ProcessingStatus = nextStatus;
    }

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
        {
            throw new DomainException("Deleted feedback cannot be changed.");
        }
    }

    private Guid GetRequiredProcessingLease()
    {
        return ProcessingLeaseId
            ?? throw new DomainException("Feedback does not have an active processing lease.");
    }

    private void EnsureActiveProcessingLease(Guid processingLeaseId)
    {
        EnsureNotDeleted();

        if (!HasActiveProcessingLease(processingLeaseId))
        {
            throw new DomainException("Feedback processing lease is no longer active.");
        }
    }

    private void ClearProcessingLease()
    {
        ProcessingLeaseId = null;
        ProcessingStartedAt = null;
    }
}
