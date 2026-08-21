using PulsePilot.Application.AI;
using PulsePilot.Domain.Feedback;
using PulsePilot.Evaluation;

namespace PulsePilot.UnitTests.Evaluation;

public sealed class EvaluationScorerTests
{
    private readonly EvaluationScorer _scorer = new();

    [Fact]
    public void Score_ExactOutputPassesStrictAndTolerantMetrics()
    {
        var evaluationCase = CreateCase();
        var actual = new FeedbackAnalysisResult(
            FeedbackCategory.Bug,
            FeedbackComponent.Payments,
            4,
            FeedbackSentiment.Negative,
            "Checkout has a duplicate charge.",
            "Investigate payment idempotency.",
            0.92m);

        var metrics = _scorer.Score(evaluationCase, actual);

        Assert.True(metrics.ContractValid);
        Assert.True(metrics.StrictPass);
        Assert.True(metrics.TolerantPass);
        Assert.Equal(1m, metrics.SummaryConceptRecall);
        Assert.Equal(1m, metrics.ActionConceptRecall);
    }

    [Fact]
    public void Score_AcceptedAlternativePassesToleranceButNotStrictness()
    {
        var evaluationCase = CreateCase(
            acceptedCategories: ["Bug", "Complaint"],
            acceptedSentiments: ["Negative", "Neutral"]);
        var actual = new FeedbackAnalysisResult(
            FeedbackCategory.Complaint,
            FeedbackComponent.Payments,
            5,
            FeedbackSentiment.Neutral,
            "Checkout has a duplicate charge.",
            "Investigate payment idempotency.",
            0.90m);

        var metrics = _scorer.Score(evaluationCase, actual);

        Assert.False(metrics.StrictCategoryMatch);
        Assert.False(metrics.StrictSentimentMatch);
        Assert.True(metrics.TolerantCategoryMatch);
        Assert.True(metrics.TolerantSentimentMatch);
        Assert.False(metrics.StrictPass);
        Assert.True(metrics.TolerantPass);
    }

    [Fact]
    public void Score_NormalizesDiacriticsAndReportsPartialConceptRecall()
    {
        var evaluationCase = CreateCase(
            summaryConcepts: ["çift ödeme", "abonelik"],
            actionConcepts: ["incele", "ödeme durumu"]);
        var actual = new FeedbackAnalysisResult(
            FeedbackCategory.Bug,
            FeedbackComponent.Payments,
            4,
            FeedbackSentiment.Negative,
            "Abonelik icin cift odeme oluştu.",
            "Odeme durumunu doğrula.",
            0.92m);

        var metrics = _scorer.Score(evaluationCase, actual);

        Assert.Equal(1m, metrics.SummaryConceptRecall);
        Assert.Equal(0.5m, metrics.ActionConceptRecall);
        Assert.False(metrics.TolerantPass);
    }

    [Fact]
    public void BuildSummary_KeepsProviderFailuresInEveryDenominator()
    {
        var passingMetrics = _scorer.Score(
            CreateCase(),
            new FeedbackAnalysisResult(
                FeedbackCategory.Bug,
                FeedbackComponent.Payments,
                4,
                FeedbackSentiment.Negative,
                "Duplicate charge in checkout.",
                "Investigate payment idempotency.",
                0.95m));
        var results = new[]
        {
            CreateResult("pass", 10, passingMetrics),
            CreateResult("fail", 30, EvaluationScorer.FailedMetrics()),
        };

        var summary = new EvaluationReportBuilder().BuildSummary(results);

        Assert.Equal(2, summary.TotalCases);
        Assert.Equal(1, summary.CompletedCases);
        Assert.Equal(1, summary.FailedCases);
        Assert.Equal(50m, summary.ContractValidityRate);
        Assert.Equal(50m, summary.TolerantPassRate);
        Assert.Equal(10, summary.Latency.P50Milliseconds);
        Assert.Equal(30, summary.Latency.P95Milliseconds);
        Assert.Equal(20m, summary.Latency.AverageMilliseconds);
    }

    private static EvaluationCase CreateCase(
        string[]? acceptedCategories = null,
        string[]? acceptedSentiments = null,
        string[]? summaryConcepts = null,
        string[]? actionConcepts = null)
    {
        return new EvaluationCase(
            "1.0",
            "fa-en-test-case-001",
            "en",
            "clear_signal",
            new EvaluationInput("Duplicate charge", "Checkout charged twice.", "Support"),
            new EvaluationExpectation(
                new CategoricalExpectation("Bug", acceptedCategories ?? ["Bug"]),
                new CategoricalExpectation("Payments", ["Payments"]),
                new SeverityExpectation(4, 5),
                new CategoricalExpectation("Negative", acceptedSentiments ?? ["Negative"]),
                summaryConcepts ?? ["duplicate charge", "checkout"],
                actionConcepts ?? ["investigate", "payment"],
                0.8m),
            ["test"]);
    }

    private static EvaluationCaseResult CreateResult(
        string id,
        long latency,
        EvaluationCaseMetrics metrics)
    {
        return new EvaluationCaseResult(
            id,
            "en",
            "clear_signal",
            ["test"],
            latency,
            Actual: null,
            metrics,
            Error: metrics.ContractValid
                ? null
                : new EvaluationError("TestFailure", "Expected failure."));
    }
}
