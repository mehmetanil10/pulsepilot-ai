using PulsePilot.Domain.Backlog;
using PulsePilot.Domain.Common;

namespace PulsePilot.UnitTests.Domain.Backlog;

public sealed class BacklogItemTests
{
    [Fact]
    public void Create_WithValidValues_CreatesOpenBacklogItem()
    {
        var workspaceId = Guid.CreateVersion7();
        var clusterId = Guid.CreateVersion7();
        var pendingActionId = Guid.CreateVersion7();
        var creatorId = Guid.CreateVersion7();
        var createdAt = DateTimeOffset.UtcNow;

        var backlogItem = BacklogItem.Create(
            workspaceId,
            clusterId,
            pendingActionId,
            creatorId,
            "  [P1] Payment failures  ",
            "  Investigate the payment failures.  ",
            BacklogItemPriority.P1,
            createdAt);

        Assert.NotEqual(Guid.Empty, backlogItem.Id);
        Assert.Equal(workspaceId, backlogItem.WorkspaceId);
        Assert.Equal(clusterId, backlogItem.SourceClusterId);
        Assert.Equal(pendingActionId, backlogItem.SourcePendingActionId);
        Assert.Equal(creatorId, backlogItem.CreatedByUserId);
        Assert.Equal("[P1] Payment failures", backlogItem.Title);
        Assert.Equal("Investigate the payment failures.", backlogItem.Description);
        Assert.Equal(BacklogItemPriority.P1, backlogItem.Priority);
        Assert.Equal(BacklogItemStatus.Open, backlogItem.Status);
        Assert.Equal(createdAt, backlogItem.CreatedAt);
    }

    [Fact]
    public void Create_RejectsInvalidIdentityContentOrPriority()
    {
        var id = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;

        Assert.Throws<DomainException>(() => BacklogItem.Create(
            Guid.Empty,
            id,
            id,
            id,
            "Title",
            "Description",
            BacklogItemPriority.P1,
            now));
        Assert.Throws<DomainException>(() => BacklogItem.Create(
            id,
            id,
            id,
            id,
            " ",
            "Description",
            BacklogItemPriority.P1,
            now));
        Assert.Throws<DomainException>(() => BacklogItem.Create(
            id,
            id,
            id,
            id,
            "Title",
            "Description",
            (BacklogItemPriority)999,
            now));
    }
}
