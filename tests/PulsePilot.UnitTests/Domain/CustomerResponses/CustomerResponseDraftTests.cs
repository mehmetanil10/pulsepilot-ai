using PulsePilot.Domain.Common;
using PulsePilot.Domain.CustomerResponses;

namespace PulsePilot.UnitTests.Domain.CustomerResponses;

public sealed class CustomerResponseDraftTests
{
    [Fact]
    public void Create_WithValidValues_TrimsAndStoresDraft()
    {
        var workspaceId = Guid.CreateVersion7();
        var feedbackId = Guid.CreateVersion7();
        var clusterId = Guid.CreateVersion7();
        var actionId = Guid.CreateVersion7();
        var creatorId = Guid.CreateVersion7();
        var createdAt = DateTimeOffset.UtcNow;

        var draft = CustomerResponseDraft.Create(
            workspaceId,
            feedbackId,
            clusterId,
            actionId,
            creatorId,
            "  We're sorry you're experiencing this issue.  ",
            createdAt);

        Assert.NotEqual(Guid.Empty, draft.Id);
        Assert.Equal(workspaceId, draft.WorkspaceId);
        Assert.Equal(feedbackId, draft.FeedbackId);
        Assert.Equal(clusterId, draft.FeedbackClusterId);
        Assert.Equal(actionId, draft.SourcePendingActionId);
        Assert.Equal(creatorId, draft.CreatedByUserId);
        Assert.Equal("We're sorry you're experiencing this issue.", draft.Content);
        Assert.Equal(createdAt, draft.CreatedAt);
    }

    [Fact]
    public void Create_RejectsMissingIdentityEmptyContentAndMoreThanMaximumWords()
    {
        var id = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        var tooManyWords = string.Join(
            ' ',
            Enumerable.Repeat("word", CustomerResponseDraft.MaxWordCount + 1));

        Assert.Throws<DomainException>(() => CustomerResponseDraft.Create(
            Guid.Empty,
            id,
            id,
            id,
            id,
            "Valid draft.",
            now));
        Assert.Throws<DomainException>(() => CustomerResponseDraft.Create(
            id,
            id,
            id,
            id,
            id,
            " ",
            now));
        Assert.Throws<DomainException>(() => CustomerResponseDraft.Create(
            id,
            id,
            id,
            id,
            id,
            tooManyWords,
            now));
    }
}
