namespace PulsePilot.Domain.Common;

public abstract class AuditableEntity : Entity
{
    protected AuditableEntity()
    {
    }

    protected AuditableEntity(Guid id, DateTimeOffset createdAt)
        : base(id)
    {
        var timestamp = Guard.UtcTimestamp(createdAt, nameof(createdAt));
        CreatedAt = timestamp;
        UpdatedAt = timestamp;
    }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    protected void MarkUpdated(DateTimeOffset updatedAt)
    {
        var timestamp = Guard.UtcTimestamp(updatedAt, nameof(updatedAt));

        if (timestamp < UpdatedAt)
        {
            throw new DomainException("Updated timestamp cannot be earlier than the current timestamp.");
        }

        UpdatedAt = timestamp;
    }
}
