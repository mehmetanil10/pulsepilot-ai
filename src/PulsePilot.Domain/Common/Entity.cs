namespace PulsePilot.Domain.Common;

public abstract class Entity
{
    protected Entity()
    {
    }

    protected Entity(Guid id)
    {
        Id = Guard.NotEmpty(id, nameof(id));
    }

    public Guid Id { get; private set; }
}
