using PulsePilot.Domain.Common;
using PulsePilot.Domain.Workspaces;

namespace PulsePilot.UnitTests.Domain.Workspaces;

public sealed class WorkspaceMemberTests
{
    private static readonly DateTimeOffset JoinedAt =
        new(2026, 8, 11, 12, 0, 0, TimeSpan.FromHours(3));

    [Fact]
    public void Join_WithValidValues_CreatesMembership()
    {
        var workspaceId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();

        var member = WorkspaceMember.Join(
            workspaceId,
            userId,
            WorkspaceRole.Admin,
            JoinedAt);

        Assert.Equal(workspaceId, member.WorkspaceId);
        Assert.Equal(userId, member.UserId);
        Assert.Equal(WorkspaceRole.Admin, member.Role);
        Assert.Equal(JoinedAt.ToUniversalTime(), member.JoinedAt);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Join_WithEmptyIdentifier_ThrowsDomainException(
        bool emptyWorkspaceId,
        bool emptyUserId)
    {
        var workspaceId = emptyWorkspaceId ? Guid.Empty : Guid.CreateVersion7();
        var userId = emptyUserId ? Guid.Empty : Guid.CreateVersion7();

        Assert.Throws<DomainException>(() =>
            WorkspaceMember.Join(workspaceId, userId, WorkspaceRole.Member, JoinedAt));
    }

    [Fact]
    public void ChangeRole_WithUnsupportedRole_ThrowsDomainException()
    {
        var member = WorkspaceMember.Join(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            WorkspaceRole.Member,
            JoinedAt);

        Assert.Throws<DomainException>(() => member.ChangeRole((WorkspaceRole)999));
        Assert.Equal(WorkspaceRole.Member, member.Role);
    }
}
