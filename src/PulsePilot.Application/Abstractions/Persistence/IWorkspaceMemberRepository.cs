using PulsePilot.Domain.Workspaces;

namespace PulsePilot.Application.Abstractions.Persistence;

public interface IWorkspaceMemberRepository
{
    Task<WorkspaceMember?> GetAsync(
        Guid workspaceId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkspaceMember>> ListByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        WorkspaceMember workspaceMember,
        CancellationToken cancellationToken = default);
}
