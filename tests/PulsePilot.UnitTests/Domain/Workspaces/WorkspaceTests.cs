using PulsePilot.Domain.Common;
using PulsePilot.Domain.Workspaces;

namespace PulsePilot.UnitTests.Domain.Workspaces;

public sealed class WorkspaceTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 8, 11, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithValidName_CreatesWorkspace()
    {
        var workspace = Workspace.Create("  PulsePilot Team  ", CreatedAt);

        Assert.NotEqual(Guid.Empty, workspace.Id);
        Assert.Equal("PulsePilot Team", workspace.Name);
        Assert.Equal(CreatedAt, workspace.CreatedAt);
        Assert.Equal(CreatedAt, workspace.UpdatedAt);
    }

    [Fact]
    public void Create_WithBlankName_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => Workspace.Create("   ", CreatedAt));
    }

    [Fact]
    public void Create_WithNameOverLimit_ThrowsDomainException()
    {
        var name = new string('a', Workspace.MaxNameLength + 1);

        Assert.Throws<DomainException>(() => Workspace.Create(name, CreatedAt));
    }

    [Fact]
    public void Rename_WithValidName_ChangesNameAndTimestamp()
    {
        var workspace = Workspace.Create("PulsePilot Team", CreatedAt);
        var updatedAt = CreatedAt.AddHours(1);

        workspace.Rename("  Product Intelligence  ", updatedAt);

        Assert.Equal("Product Intelligence", workspace.Name);
        Assert.Equal(updatedAt, workspace.UpdatedAt);
    }
}
