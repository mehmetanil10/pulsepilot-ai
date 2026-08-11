using Microsoft.EntityFrameworkCore;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Domain.Workspaces;

namespace PulsePilot.Infrastructure.Persistence.Repositories;

internal sealed class WorkspaceRepository : IWorkspaceRepository
{
    private readonly AppDbContext _dbContext;

    public WorkspaceRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Workspace?> GetByIdAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Workspaces.SingleOrDefaultAsync(
            workspace => workspace.Id == workspaceId,
            cancellationToken);
    }

    public async Task AddAsync(
        Workspace workspace,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.Workspaces.AddAsync(workspace, cancellationToken);
    }
}
