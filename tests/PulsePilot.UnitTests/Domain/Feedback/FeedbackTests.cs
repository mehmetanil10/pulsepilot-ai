using PulsePilot.Domain.Common;
using PulsePilot.Domain.Feedback;

using FeedbackEntity = PulsePilot.Domain.Feedback.Feedback;

namespace PulsePilot.UnitTests.Domain.Feedback;

public sealed class FeedbackTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 8, 11, 12, 0, 0, TimeSpan.FromHours(3));

    [Fact]
    public void Create_WithValidValues_CreatesPendingFeedback()
    {
        var workspaceId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();

        var feedback = FeedbackEntity.Create(
            workspaceId,
            userId,
            "  Payment problem  ",
            "  I cannot add my credit card.  ",
            FeedbackSource.Manual,
            "  Example Customer  ",
            "  customer@example.com  ",
            CreatedAt);

        Assert.NotEqual(Guid.Empty, feedback.Id);
        Assert.Equal(workspaceId, feedback.WorkspaceId);
        Assert.Equal(userId, feedback.CreatedByUserId);
        Assert.Equal("Payment problem", feedback.Title);
        Assert.Equal("I cannot add my credit card.", feedback.Content);
        Assert.Equal(FeedbackSource.Manual, feedback.Source);
        Assert.Equal("Example Customer", feedback.CustomerName);
        Assert.Equal("customer@example.com", feedback.CustomerEmail);
        Assert.Equal(ProcessingStatus.Pending, feedback.ProcessingStatus);
        Assert.False(feedback.IsDeleted);
        Assert.Equal(CreatedAt.ToUniversalTime(), feedback.CreatedAt);
    }

    [Fact]
    public void Create_WithBlankOptionalValues_StoresNulls()
    {
        var feedback = FeedbackEntity.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            " ",
            "Dashboard is slow.",
            FeedbackSource.Api,
            " ",
            null,
            CreatedAt);

        Assert.Null(feedback.Title);
        Assert.Null(feedback.CustomerName);
        Assert.Null(feedback.CustomerEmail);
    }

    [Fact]
    public void Create_WithBlankContent_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() =>
            FeedbackEntity.Create(
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                null,
                "   ",
                FeedbackSource.Manual,
                null,
                null,
                CreatedAt));
    }

    [Fact]
    public void Create_WithContentOverLimit_ThrowsDomainException()
    {
        var content = new string('a', FeedbackEntity.MaxContentLength + 1);

        Assert.Throws<DomainException>(() =>
            FeedbackEntity.Create(
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                null,
                content,
                FeedbackSource.Manual,
                null,
                null,
                CreatedAt));
    }

    [Fact]
    public void Create_WithInvalidCustomerEmail_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() =>
            FeedbackEntity.Create(
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                null,
                "Dashboard is slow.",
                FeedbackSource.Manual,
                null,
                "invalid-email",
                CreatedAt));
    }

    [Fact]
    public void ProcessingTransitions_FollowExpectedStateMachine()
    {
        var feedback = CreateFeedback();

        var processingLeaseId = feedback.StartProcessing(CreatedAt.AddMinutes(1));
        Assert.Equal(ProcessingStatus.Processing, feedback.ProcessingStatus);
        Assert.Equal(processingLeaseId, feedback.ProcessingLeaseId);
        Assert.Equal(CreatedAt.AddMinutes(1).ToUniversalTime(), feedback.ProcessingStartedAt);
        Assert.True(feedback.HasActiveProcessingLease(processingLeaseId));

        feedback.CompleteProcessing(CreatedAt.AddMinutes(2));
        Assert.Equal(ProcessingStatus.Completed, feedback.ProcessingStatus);
        Assert.Null(feedback.ProcessingLeaseId);
        Assert.Null(feedback.ProcessingStartedAt);
        Assert.Equal(CreatedAt.AddMinutes(2).ToUniversalTime(), feedback.UpdatedAt);
    }

    [Fact]
    public void CompleteProcessing_WhenPending_ThrowsDomainException()
    {
        var feedback = CreateFeedback();

        Assert.Throws<DomainException>(() =>
            feedback.CompleteProcessing(CreatedAt.AddMinutes(1)));

        Assert.Equal(ProcessingStatus.Pending, feedback.ProcessingStatus);
    }

    [Fact]
    public void FailedFeedback_CanBeRetried()
    {
        var feedback = CreateFeedback();
        feedback.StartProcessing(CreatedAt.AddMinutes(1));
        feedback.FailProcessing(CreatedAt.AddMinutes(2));

        feedback.RetryProcessing(CreatedAt.AddMinutes(3));

        Assert.Equal(ProcessingStatus.Pending, feedback.ProcessingStatus);
        Assert.Null(feedback.ProcessingLeaseId);
        Assert.Null(feedback.ProcessingStartedAt);
    }

    [Fact]
    public void Completion_WithExpiredLease_ThrowsDomainException()
    {
        var feedback = CreateFeedback();
        var expiredLeaseId = feedback.StartProcessing(CreatedAt.AddMinutes(1));
        feedback.FailProcessing(expiredLeaseId, CreatedAt.AddMinutes(2));
        feedback.RetryProcessing(CreatedAt.AddMinutes(3));
        var activeLeaseId = feedback.StartProcessing(CreatedAt.AddMinutes(4));

        Assert.Throws<DomainException>(() =>
            feedback.CompleteProcessing(expiredLeaseId, CreatedAt.AddMinutes(5)));

        Assert.Equal(ProcessingStatus.Processing, feedback.ProcessingStatus);
        Assert.True(feedback.HasActiveProcessingLease(activeLeaseId));
    }

    [Fact]
    public void UpdateDetails_AfterCompletion_ResetsProcessingStatus()
    {
        var feedback = CreateFeedback();
        feedback.StartProcessing(CreatedAt.AddMinutes(1));
        feedback.AssignToCluster(Guid.CreateVersion7(), CreatedAt.AddMinutes(2));
        feedback.CompleteProcessing(CreatedAt.AddMinutes(2));

        feedback.UpdateDetails(
            "  Updated title  ",
            "  Updated content  ",
            FeedbackSource.Support,
            "  Updated customer  ",
            "updated@example.com",
            CreatedAt.AddMinutes(3));

        Assert.Equal("Updated title", feedback.Title);
        Assert.Equal("Updated content", feedback.Content);
        Assert.Equal(FeedbackSource.Support, feedback.Source);
        Assert.Equal("Updated customer", feedback.CustomerName);
        Assert.Equal("updated@example.com", feedback.CustomerEmail);
        Assert.Equal(ProcessingStatus.Pending, feedback.ProcessingStatus);
        Assert.Null(feedback.FeedbackClusterId);
    }

    [Fact]
    public void AssignToCluster_IsIdempotentButRejectsReassignment()
    {
        var feedback = CreateFeedback();
        var clusterId = Guid.CreateVersion7();

        feedback.AssignToCluster(clusterId, CreatedAt.AddMinutes(1));
        feedback.AssignToCluster(clusterId, CreatedAt.AddMinutes(2));

        Assert.Equal(clusterId, feedback.FeedbackClusterId);
        Assert.Throws<DomainException>(() => feedback.AssignToCluster(
            Guid.CreateVersion7(),
            CreatedAt.AddMinutes(3)));
    }

    [Fact]
    public void UpdateDetails_WhileProcessing_ThrowsDomainException()
    {
        var feedback = CreateFeedback();
        feedback.StartProcessing(CreatedAt.AddMinutes(1));

        Assert.Throws<DomainException>(() =>
            feedback.UpdateDetails(
                null,
                "Updated content",
                FeedbackSource.Manual,
                null,
                null,
                CreatedAt.AddMinutes(2)));

        Assert.Equal("Dashboard is slow.", feedback.Content);
        Assert.Equal(ProcessingStatus.Processing, feedback.ProcessingStatus);
    }

    [Fact]
    public void MarkDeleted_PreventsFurtherChanges()
    {
        var feedback = CreateFeedback();
        var deletedAt = CreatedAt.AddMinutes(1);

        feedback.MarkDeleted(deletedAt);

        Assert.True(feedback.IsDeleted);
        Assert.Equal(deletedAt.ToUniversalTime(), feedback.DeletedAt);
        Assert.Throws<DomainException>(() =>
            feedback.StartProcessing(CreatedAt.AddMinutes(2)));
    }

    [Fact]
    public void MarkDeleted_WhenAlreadyDeleted_IsIdempotent()
    {
        var feedback = CreateFeedback();
        var deletedAt = CreatedAt.AddMinutes(1);

        feedback.MarkDeleted(deletedAt);
        feedback.MarkDeleted(CreatedAt.AddMinutes(2));

        Assert.Equal(deletedAt.ToUniversalTime(), feedback.DeletedAt);
        Assert.Equal(deletedAt.ToUniversalTime(), feedback.UpdatedAt);
    }

    private static FeedbackEntity CreateFeedback()
    {
        return FeedbackEntity.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "Dashboard performance",
            "Dashboard is slow.",
            FeedbackSource.Manual,
            null,
            null,
            CreatedAt);
    }
}
