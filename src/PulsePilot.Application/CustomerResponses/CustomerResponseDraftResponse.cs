using PulsePilot.Domain.CustomerResponses;

namespace PulsePilot.Application.CustomerResponses;

public sealed record CustomerResponseDraftResponse(
    Guid Id,
    Guid FeedbackId,
    Guid FeedbackClusterId,
    Guid SourcePendingActionId,
    Guid CreatedByUserId,
    string Content,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    internal static CustomerResponseDraftResponse FromEntity(CustomerResponseDraft draft)
    {
        return new CustomerResponseDraftResponse(
            draft.Id,
            draft.FeedbackId,
            draft.FeedbackClusterId,
            draft.SourcePendingActionId,
            draft.CreatedByUserId,
            draft.Content,
            draft.CreatedAt,
            draft.UpdatedAt);
    }
}
