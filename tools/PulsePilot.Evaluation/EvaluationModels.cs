using PulsePilot.Application.AI;

namespace PulsePilot.Evaluation;

public sealed record EvaluationCase(
    string SchemaVersion,
    string Id,
    string Language,
    string Scenario,
    EvaluationInput Input,
    EvaluationExpectation Expected,
    string[] Tags);

public sealed record EvaluationInput(
    string? Title,
    string Content,
    string Source);

public sealed record EvaluationExpectation(
    CategoricalExpectation Category,
    CategoricalExpectation Component,
    SeverityExpectation Severity,
    CategoricalExpectation Sentiment,
    string[] RequiredSummaryConcepts,
    string[] RequiredActionConcepts,
    decimal MinimumConfidence);

public sealed record CategoricalExpectation(
    string Preferred,
    string[] Accepted);

public sealed record SeverityExpectation(int Minimum, int Maximum);

public sealed record EvaluationManifest(
    string Name,
    string Version,
    string SchemaVersion,
    string File,
    int CaseCount,
    Dictionary<string, int> Languages,
    Dictionary<string, int> Scenarios,
    EvaluationDataPolicy DataPolicy);

public sealed record EvaluationDataPolicy(
    bool SyntheticOnly,
    bool RealCustomerDataAllowed,
    bool ExternalProviderCallsRequired);

public sealed record LoadedEvaluationDataset(
    string ManifestPath,
    EvaluationManifest Manifest,
    IReadOnlyList<EvaluationCase> Cases);

public sealed record EvaluationCaseMetrics(
    bool ContractValid,
    bool StrictCategoryMatch,
    bool TolerantCategoryMatch,
    bool StrictComponentMatch,
    bool TolerantComponentMatch,
    bool StrictSentimentMatch,
    bool TolerantSentimentMatch,
    bool SeverityWithinRange,
    decimal SummaryConceptRecall,
    decimal ActionConceptRecall,
    bool ConfidenceMeetsFloor,
    bool StrictPass,
    bool TolerantPass);

public sealed record EvaluationError(string Type, string Message);

public sealed record EvaluationCaseResult(
    string Id,
    string Language,
    string Scenario,
    string[] Tags,
    long LatencyMilliseconds,
    FeedbackAnalysisResult? Actual,
    EvaluationCaseMetrics Metrics,
    EvaluationError? Error);

public sealed record EvaluationLatencyMetrics(
    decimal AverageMilliseconds,
    long P50Milliseconds,
    long P95Milliseconds,
    long MaximumMilliseconds);

public sealed record EvaluationSummary(
    int TotalCases,
    int CompletedCases,
    int FailedCases,
    decimal ContractValidityRate,
    decimal StrictCategoryAccuracy,
    decimal TolerantCategoryAccuracy,
    decimal StrictComponentAccuracy,
    decimal TolerantComponentAccuracy,
    decimal StrictSentimentAccuracy,
    decimal TolerantSentimentAccuracy,
    decimal SeverityAccuracy,
    decimal SummaryConceptRecall,
    decimal ActionConceptRecall,
    decimal ConfidenceFloorRate,
    decimal StrictPassRate,
    decimal TolerantPassRate,
    EvaluationLatencyMetrics Latency);

public sealed record EvaluationBreakdown(
    string Value,
    int TotalCases,
    int CompletedCases,
    decimal ContractValidityRate,
    decimal StrictPassRate,
    decimal TolerantPassRate);

public sealed record EvaluationUsage(
    string Status,
    string Reason,
    int? InputTokens,
    int? OutputTokens,
    decimal? EstimatedCostUsd);

public sealed record EvaluationGate(
    string Status,
    IReadOnlyList<string> Failures);

public sealed record EvaluationReport(
    string SchemaVersion,
    Guid RunId,
    DateTimeOffset GeneratedAtUtc,
    string DatasetName,
    string DatasetVersion,
    string DatasetSchemaVersion,
    string Provider,
    string Model,
    bool IsModelEvaluation,
    int SelectedCaseCount,
    EvaluationSummary Summary,
    IReadOnlyList<EvaluationBreakdown> Languages,
    IReadOnlyList<EvaluationBreakdown> Scenarios,
    EvaluationUsage Usage,
    EvaluationGate Gate,
    IReadOnlyList<EvaluationCaseResult> Cases);
