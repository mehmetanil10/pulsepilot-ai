using PulsePilot.Application.Feedback;
using PulsePilot.Domain.Feedback;

namespace PulsePilot.UnitTests.Application.Feedback;

public sealed class FeedbackValidatorTests
{
    [Fact]
    public async Task CreateCommand_WithValidManualFeedback_IsValid()
    {
        var command = new CreateFeedbackCommand(
            "Payment problem",
            "I cannot add my card after the latest update.",
            FeedbackSource.Manual,
            "Example Customer",
            "customer@example.com");

        var result = await new CreateFeedbackCommandValidator().ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("", FeedbackSource.Manual, "customer@example.com")]
    [InlineData("Valid content", FeedbackSource.Email, "customer@example.com")]
    [InlineData("Valid content", FeedbackSource.Api, "invalid-email")]
    public async Task CreateCommand_WithInvalidValues_IsRejected(
        string content,
        FeedbackSource source,
        string customerEmail)
    {
        var command = new CreateFeedbackCommand(
            null,
            content,
            source,
            null,
            customerEmail);

        var result = await new CreateFeedbackCommandValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task UpdateCommand_WithValidApiFeedback_IsValid()
    {
        var command = new UpdateFeedbackCommand(
            "Updated title",
            "Updated content",
            FeedbackSource.Api,
            null,
            null);

        var result = await new UpdateFeedbackCommandValidator().ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, ListFeedbackQueryValidator.MaximumPageSize + 1)]
    [InlineData(int.MaxValue, ListFeedbackQueryValidator.MaximumPageSize)]
    public async Task ListQuery_WithUnsupportedPagination_IsRejected(int page, int pageSize)
    {
        var query = new ListFeedbackQuery
        {
            Page = page,
            PageSize = pageSize,
        };

        var result = await new ListFeedbackQueryValidator().ValidateAsync(query);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ListQuery_WithUnsupportedAnalysisAndDateFilters_IsRejected()
    {
        var query = new ListFeedbackQuery
        {
            Category = (FeedbackCategory)999,
            Component = (FeedbackComponent)999,
            Sentiment = (FeedbackSentiment)999,
            Severity = 6,
            DateFrom = new DateOnly(2026, 8, 13),
            DateTo = new DateOnly(2026, 8, 1),
            Search = new string('x', ListFeedbackQueryValidator.MaximumSearchLength + 1),
        };

        var result = await new ListFeedbackQueryValidator().ValidateAsync(query);

        Assert.False(result.IsValid);
        Assert.Equal(6, result.Errors.Count);
    }
}
