using PulsePilot.Domain.Common;

namespace PulsePilot.Domain.Workspaces;

public sealed class Workspace : AuditableEntity
{
    public const int MaxNameLength = 150;

    private Workspace()
    {
    }

    private Workspace(Guid id, string name, DateTimeOffset createdAt)
        : base(id, createdAt)
    {
        Name = Guard.RequiredText(name, nameof(name), MaxNameLength);
    }

    public string Name { get; private set; } = null!;

    public static Workspace Create(string name, DateTimeOffset createdAt)
    {
        return new Workspace(Guid.CreateVersion7(), name, createdAt);
    }

    public void Rename(string name, DateTimeOffset updatedAt)
    {
        var validatedName = Guard.RequiredText(name, nameof(name), MaxNameLength);

        MarkUpdated(updatedAt);
        Name = validatedName;
    }
}
