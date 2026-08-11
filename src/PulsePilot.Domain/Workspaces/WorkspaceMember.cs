using PulsePilot.Domain.Common;

namespace PulsePilot.Domain.Workspaces;

public sealed class WorkspaceMember
{
    private WorkspaceMember()
    {
    }

    private WorkspaceMember(
        Guid workspaceId,
        Guid userId,
        WorkspaceRole role,
        DateTimeOffset joinedAt)
    {
        WorkspaceId = Guard.NotEmpty(workspaceId, nameof(workspaceId));
        UserId = Guard.NotEmpty(userId, nameof(userId));
        Role = Guard.DefinedEnum(role, nameof(role));
        JoinedAt = Guard.UtcTimestamp(joinedAt, nameof(joinedAt));
    }

    public Guid WorkspaceId { get; private set; }

    public Guid UserId { get; private set; }

    public WorkspaceRole Role { get; private set; }

    public DateTimeOffset JoinedAt { get; private set; }

    public static WorkspaceMember Join(
        Guid workspaceId,
        Guid userId,
        WorkspaceRole role,
        DateTimeOffset joinedAt)
    {
        return new WorkspaceMember(workspaceId, userId, role, joinedAt);
    }

    public void ChangeRole(WorkspaceRole role)
    {
        Role = Guard.DefinedEnum(role, nameof(role));
    }
}
