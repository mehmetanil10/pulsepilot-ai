using PulsePilot.Domain.CustomerResponses;

namespace PulsePilot.Application.Abstractions.Persistence;

public interface ICustomerResponseDraftRepository
{
    Task<CustomerResponseDraft?> GetBySourcePendingActionIdAsync(
        Guid workspaceId,
        Guid sourcePendingActionId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        CustomerResponseDraft draft,
        CancellationToken cancellationToken = default);
}
