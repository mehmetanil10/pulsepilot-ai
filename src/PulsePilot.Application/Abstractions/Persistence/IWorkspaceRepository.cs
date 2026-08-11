using PulsePilot.Domain.Workspaces;

namespace PulsePilot.Application.Abstractions.Persistence;

public interface IWorkspaceRepository
{
    Task<Workspace?> GetByIdAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);

    Task AddAsync(Workspace workspace, CancellationToken cancellationToken = default);
}
