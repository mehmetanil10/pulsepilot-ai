using PulsePilot.Domain.Actions;
using PulsePilot.Domain.Common;

namespace PulsePilot.UnitTests.Domain.Actions;

public sealed class PendingActionTests
{
    [Fact]
    public void Create_WithValidValues_CreatesPendingAction()
    {
        var workspaceId = Guid.CreateVersion7();
        var feedbackId = Guid.CreateVersion7();
        var clusterId = Guid.CreateVersion7();
        var createdAt = DateTimeOffset.UtcNow;

        var pendingAction = PendingAction.Create(
            workspaceId,
            feedbackId,
            clusterId,
            PendingActionType.CreateEngineeringIssue,
            "  [P1] Payment failures  ",
            "  Create an engineering issue.  ",
            "  {\"priority\":\"p1\"}  ",
            createdAt);

        Assert.NotEqual(Guid.Empty, pendingAction.Id);
        Assert.Equal(workspaceId, pendingAction.WorkspaceId);
        Assert.Equal(feedbackId, pendingAction.FeedbackId);
        Assert.Equal(clusterId, pendingAction.FeedbackClusterId);
        Assert.Equal(PendingActionType.CreateEngineeringIssue, pendingAction.ActionType);
        Assert.Equal("[P1] Payment failures", pendingAction.Title);
        Assert.Equal("Create an engineering issue.", pendingAction.Description);
        Assert.Equal("{\"priority\":\"p1\"}", pendingAction.Payload);
        Assert.Equal(PendingActionStatus.Pending, pendingAction.Status);
        Assert.Null(pendingAction.ApprovedAt);
        Assert.Null(pendingAction.RejectedAt);
        Assert.Null(pendingAction.ExecutedAt);
        Assert.Equal(createdAt, pendingAction.CreatedAt);
    }

    [Fact]
    public void Create_RejectsInvalidIdentityActionTypeOrContent()
    {
        var now = DateTimeOffset.UtcNow;
        var id = Guid.CreateVersion7();

        Assert.Throws<DomainException>(() => PendingAction.Create(
            Guid.Empty,
            id,
            id,
            PendingActionType.CreateEngineeringIssue,
            "Title",
            "Description",
            "{}",
            now));
        Assert.Throws<DomainException>(() => PendingAction.Create(
            id,
            id,
            id,
            (PendingActionType)999,
            "Title",
            "Description",
            "{}",
            now));
        Assert.Throws<DomainException>(() => PendingAction.Create(
            id,
            id,
            id,
            PendingActionType.CreateEngineeringIssue,
            " ",
            "Description",
            "{}",
            now));
        Assert.Throws<DomainException>(() => PendingAction.Create(
            id,
            id,
            id,
            PendingActionType.CreateEngineeringIssue,
            "Title",
            "Description",
            " ",
            now));
    }

    [Fact]
    public void Approve_WhenPending_RecordsDecisionAndIsIdempotent()
    {
        var createdAt = DateTimeOffset.UtcNow;
        var approvedAt = createdAt.AddMinutes(1);
        var pendingAction = CreatePendingAction(createdAt);

        pendingAction.Approve(approvedAt);
        pendingAction.Approve(approvedAt.AddMinutes(1));

        Assert.Equal(PendingActionStatus.Approved, pendingAction.Status);
        Assert.Equal(approvedAt, pendingAction.ApprovedAt);
        Assert.Null(pendingAction.RejectedAt);
        Assert.Equal(approvedAt, pendingAction.UpdatedAt);
    }

    [Fact]
    public void Reject_WhenPending_RecordsDecisionAndIsIdempotent()
    {
        var createdAt = DateTimeOffset.UtcNow;
        var rejectedAt = createdAt.AddMinutes(1);
        var pendingAction = CreatePendingAction(createdAt);

        pendingAction.Reject(rejectedAt);
        pendingAction.Reject(rejectedAt.AddMinutes(1));

        Assert.Equal(PendingActionStatus.Rejected, pendingAction.Status);
        Assert.Equal(rejectedAt, pendingAction.RejectedAt);
        Assert.Null(pendingAction.ApprovedAt);
        Assert.Equal(rejectedAt, pendingAction.UpdatedAt);
    }

    [Fact]
    public void Review_AfterOppositeDecision_IsRejectedWithoutMutation()
    {
        var createdAt = DateTimeOffset.UtcNow;
        var approvedAt = createdAt.AddMinutes(1);
        var pendingAction = CreatePendingAction(createdAt);
        pendingAction.Approve(approvedAt);

        Assert.Throws<DomainException>(() =>
            pendingAction.Reject(approvedAt.AddMinutes(1)));
        Assert.Equal(PendingActionStatus.Approved, pendingAction.Status);
        Assert.Equal(approvedAt, pendingAction.ApprovedAt);
        Assert.Null(pendingAction.RejectedAt);
        Assert.Equal(approvedAt, pendingAction.UpdatedAt);
    }

    private static PendingAction CreatePendingAction(DateTimeOffset createdAt)
    {
        return PendingAction.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            PendingActionType.CreateEngineeringIssue,
            "[P1] Payment failures",
            "Create an engineering issue.",
            "{}",
            createdAt);
    }
}
