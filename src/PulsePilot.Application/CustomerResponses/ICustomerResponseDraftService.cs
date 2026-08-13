namespace PulsePilot.Application.CustomerResponses;

public interface ICustomerResponseDraftService
{
    Task<CustomerResponseDraftResponse> GetByPendingActionIdAsync(
        Guid pendingActionId,
        CancellationToken cancellationToken = default);
}
