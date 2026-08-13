using Microsoft.EntityFrameworkCore;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Domain.CustomerResponses;

namespace PulsePilot.Infrastructure.Persistence.Repositories;

internal sealed class CustomerResponseDraftRepository(AppDbContext dbContext)
    : ICustomerResponseDraftRepository
{
    public Task<CustomerResponseDraft?> GetBySourcePendingActionIdAsync(
        Guid workspaceId,
        Guid sourcePendingActionId,
        CancellationToken cancellationToken = default)
    {
        var trackedDraft = dbContext.CustomerResponseDrafts.Local.SingleOrDefault(
            draft => draft.WorkspaceId == workspaceId
                && draft.SourcePendingActionId == sourcePendingActionId);

        if (trackedDraft is not null)
        {
            return Task.FromResult<CustomerResponseDraft?>(trackedDraft);
        }

        return dbContext.CustomerResponseDrafts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                draft => draft.WorkspaceId == workspaceId
                    && draft.SourcePendingActionId == sourcePendingActionId,
                cancellationToken);
    }

    public async Task AddAsync(
        CustomerResponseDraft draft,
        CancellationToken cancellationToken = default)
    {
        await dbContext.CustomerResponseDrafts.AddAsync(draft, cancellationToken);
    }
}
