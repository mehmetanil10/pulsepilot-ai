using PulsePilot.Domain.Common;
using PulsePilot.Domain.Feedback;

namespace PulsePilot.UnitTests.Domain.Feedback;

public sealed class FeedbackAnalysisTests
{
    private static readonly DateTimeOffset AnalyzedAt =
        new(2026, 8, 12, 9, 0, 0, TimeSpan.FromHours(3));

    [Fact]
    public void Create_WithStructuredResult_CreatesAnalysis()
    {
        var workspaceId = Guid.CreateVersion7();
        var feedbackId = Guid.CreateVersion7();

        var analysis = FeedbackAnalysis.Create(
            workspaceId,
            feedbackId,
            FeedbackCategory.Bug,
            FeedbackComponent.Payments,
            4,
            FeedbackSentiment.Negative,
            "  User cannot add a payment card.  ",
            "  Investigate the payment service.  ",
            0.94m,
            AnalyzedAt);

        Assert.NotEqual(Guid.Empty, analysis.Id);
        Assert.Equal(workspaceId, analysis.WorkspaceId);
        Assert.Equal(feedbackId, analysis.FeedbackId);
        Assert.Equal(FeedbackCategory.Bug, analysis.Category);
        Assert.Equal(FeedbackComponent.Payments, analysis.Component);
        Assert.Equal(4, analysis.Severity);
        Assert.Equal(FeedbackSentiment.Negative, analysis.Sentiment);
        Assert.Equal("User cannot add a payment card.", analysis.Summary);
        Assert.Equal("Investigate the payment service.", analysis.SuggestedAction);
        Assert.Equal(0.94m, analysis.Confidence);
        Assert.Equal(AnalyzedAt.ToUniversalTime(), analysis.CreatedAt);
    }

    [Fact]
    public void ReplaceResult_WithValidValues_ReplacesStructuredResult()
    {
        var analysis = CreateAnalysis();
        var updatedAt = AnalyzedAt.AddMinutes(5);

        analysis.ReplaceResult(
            FeedbackCategory.FeatureRequest,
            FeedbackComponent.Dashboard,
            2,
            FeedbackSentiment.Positive,
            "Customer requests a saved dashboard view.",
            "Evaluate the request for the product roadmap.",
            0.88m,
            updatedAt);

        Assert.Equal(FeedbackCategory.FeatureRequest, analysis.Category);
        Assert.Equal(FeedbackComponent.Dashboard, analysis.Component);
        Assert.Equal(2, analysis.Severity);
        Assert.Equal(FeedbackSentiment.Positive, analysis.Sentiment);
        Assert.Equal(0.88m, analysis.Confidence);
        Assert.Equal(updatedAt.ToUniversalTime(), analysis.UpdatedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Create_WithSeverityOutsideRange_ThrowsDomainException(int severity)
    {
        Assert.Throws<DomainException>(() =>
            CreateAnalysis(severity: severity));
    }

    [Theory]
    [InlineData("-0.01")]
    [InlineData("1.01")]
    public void Create_WithConfidenceOutsideRange_ThrowsDomainException(string value)
    {
        var confidence = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Throws<DomainException>(() =>
            CreateAnalysis(confidence: confidence));
    }

    [Fact]
    public void ReplaceResult_WithInvalidResult_DoesNotChangeAnalysis()
    {
        var analysis = CreateAnalysis();

        Assert.Throws<DomainException>(() =>
            analysis.ReplaceResult(
                FeedbackCategory.Question,
                FeedbackComponent.General,
                3,
                FeedbackSentiment.Neutral,
                " ",
                "Answer the customer question.",
                0.7m,
                AnalyzedAt.AddMinutes(1)));

        Assert.Equal(FeedbackCategory.Bug, analysis.Category);
        Assert.Equal("User cannot add a payment card.", analysis.Summary);
        Assert.Equal(AnalyzedAt.ToUniversalTime(), analysis.UpdatedAt);
    }

    private static FeedbackAnalysis CreateAnalysis(
        int severity = 4,
        decimal confidence = 0.94m)
    {
        return FeedbackAnalysis.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            FeedbackCategory.Bug,
            FeedbackComponent.Payments,
            severity,
            FeedbackSentiment.Negative,
            "User cannot add a payment card.",
            "Investigate the payment service.",
            confidence,
            AnalyzedAt);
    }
}
