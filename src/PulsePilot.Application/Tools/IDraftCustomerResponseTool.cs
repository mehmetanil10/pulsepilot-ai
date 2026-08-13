using PulsePilot.Domain.Actions;
using PulsePilot.Domain.CustomerResponses;

namespace PulsePilot.Application.Tools;

public interface IDraftCustomerResponseTool
{
    Task<CustomerResponseDraft> ExecuteAsync(
        PendingAction pendingAction,
        Guid createdByUserId,
        DateTimeOffset executedAt,
        CancellationToken cancellationToken = default);
}
