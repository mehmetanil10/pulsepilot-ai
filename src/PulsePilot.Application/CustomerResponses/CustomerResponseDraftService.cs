using PulsePilot.Application.Abstractions.Authentication;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Application.Common.Exceptions;

namespace PulsePilot.Application.CustomerResponses;

internal sealed class CustomerResponseDraftService(
    ICustomerResponseDraftRepository customerResponseDraftRepository,
    ICurrentUserContext currentUser) : ICustomerResponseDraftService
{
    public async Task<CustomerResponseDraftResponse> GetByPendingActionIdAsync(
        Guid pendingActionId,
        CancellationToken cancellationToken = default)
    {
        var draft = await customerResponseDraftRepository.GetBySourcePendingActionIdAsync(
            currentUser.WorkspaceId,
            pendingActionId,
            cancellationToken)
            ?? throw new NotFoundException("CustomerResponseDraft", pendingActionId);

        return CustomerResponseDraftResponse.FromEntity(draft);
    }
}
