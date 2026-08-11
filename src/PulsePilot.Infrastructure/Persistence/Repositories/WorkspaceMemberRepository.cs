using Microsoft.EntityFrameworkCore;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Domain.Workspaces;

namespace PulsePilot.Infrastructure.Persistence.Repositories;

internal sealed class WorkspaceMemberRepository : IWorkspaceMemberRepository
{
    private readonly AppDbContext _dbContext;

    public WorkspaceMemberRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<WorkspaceMember?> GetAsync(
        Guid workspaceId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.WorkspaceMembers.SingleOrDefaultAsync(
            member => member.WorkspaceId == workspaceId && member.UserId == userId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<WorkspaceMember>> ListByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.WorkspaceMembers
            .AsNoTracking()
            .Where(member => member.UserId == userId)
            .OrderBy(member => member.WorkspaceId)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        WorkspaceMember workspaceMember,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.WorkspaceMembers.AddAsync(workspaceMember, cancellationToken);
    }
}
