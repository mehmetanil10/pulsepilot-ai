using PulsePilot.Domain.Common;
using PulsePilot.Domain.Feedback;

namespace PulsePilot.UnitTests.Domain.Feedback;

public sealed class FeedbackClusterTests
{
    [Fact]
    public void Create_WithValidValues_CreatesStructuredCluster()
    {
        var workspaceId = Guid.CreateVersion7();
        var createdAt = DateTimeOffset.UtcNow;

        var cluster = FeedbackCluster.Create(
            workspaceId,
            "  Payment card failures  ",
            FeedbackCategory.Bug,
            FeedbackComponent.Payments,
            createdAt);

        Assert.NotEqual(Guid.Empty, cluster.Id);
        Assert.Equal(workspaceId, cluster.WorkspaceId);
        Assert.Equal("Payment card failures", cluster.Title);
        Assert.Equal(FeedbackCategory.Bug, cluster.Category);
        Assert.Equal(FeedbackComponent.Payments, cluster.Component);
        Assert.Equal(createdAt, cluster.CreatedAt);
    }

    [Fact]
    public void Create_RejectsInvalidTitleOrClassification()
    {
        Assert.Throws<DomainException>(() => FeedbackCluster.Create(
            Guid.CreateVersion7(),
            " ",
            FeedbackCategory.Bug,
            FeedbackComponent.Payments,
            DateTimeOffset.UtcNow));
        Assert.Throws<DomainException>(() => FeedbackCluster.Create(
            Guid.CreateVersion7(),
            "Payment failures",
            (FeedbackCategory)999,
            FeedbackComponent.Payments,
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void RecordActivity_AdvancesUpdatedTimestamp()
    {
        var createdAt = DateTimeOffset.UtcNow;
        var cluster = FeedbackCluster.Create(
            Guid.CreateVersion7(),
            "Payment failures",
            FeedbackCategory.Bug,
            FeedbackComponent.Payments,
            createdAt);

        cluster.RecordActivity(createdAt.AddMinutes(1));

        Assert.Equal(createdAt.AddMinutes(1), cluster.UpdatedAt);
    }
}
