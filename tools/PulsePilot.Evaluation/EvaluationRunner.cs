using System.Diagnostics;

namespace PulsePilot.Evaluation;

public sealed class EvaluationRunner(
    EvaluationScorer scorer,
    EvaluationReportBuilder reportBuilder)
{
    public async Task<EvaluationReport> RunAsync(
        LoadedEvaluationDataset dataset,
        IEvaluationProvider provider,
        RunnerOptions options,
        TextWriter progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(progress);

        var selectedCases = SelectCases(dataset.Cases, options.Limit);
        var results = new List<EvaluationCaseResult>(selectedCases.Length);

        for (var index = 0; index < selectedCases.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var evaluationCase = selectedCases[index];
            await progress.WriteAsync(
                $"[{index + 1}/{selectedCases.Length}] {evaluationCase.Id} ... ");
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
            timeoutSource.CancelAfter(TimeSpan.FromSeconds(options.CaseTimeoutSeconds));
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var actual = await provider.AnalyzeAsync(
                    evaluationCase,
                    timeoutSource.Token);
                stopwatch.Stop();
                var metrics = scorer.Score(evaluationCase, actual);
                results.Add(new EvaluationCaseResult(
                    evaluationCase.Id,
                    evaluationCase.Language,
                    evaluationCase.Scenario,
                    evaluationCase.Tags,
                    stopwatch.ElapsedMilliseconds,
                    actual,
                    metrics,
                    Error: null));
                await progress.WriteLineAsync(
                    metrics.TolerantPass ? "PASS" : "SCORED");
            }
            catch (Exception exception) when (
                exception is not OperationCanceledException
                || !cancellationToken.IsCancellationRequested)
            {
                stopwatch.Stop();
                results.Add(new EvaluationCaseResult(
                    evaluationCase.Id,
                    evaluationCase.Language,
                    evaluationCase.Scenario,
                    evaluationCase.Tags,
                    stopwatch.ElapsedMilliseconds,
                    Actual: null,
                    EvaluationScorer.FailedMetrics(),
                    new EvaluationError(
                        exception.GetType().Name,
                        SafeErrorMessage(exception))));
                await progress.WriteLineAsync("ERROR");
            }
        }

        var summary = reportBuilder.BuildSummary(results);
        var gateFailures = BuildGateFailures(summary, options);

        return new EvaluationReport(
            SchemaVersion: "1.0",
            RunId: Guid.CreateVersion7(),
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            DatasetName: dataset.Manifest.Name,
            DatasetVersion: dataset.Manifest.Version,
            DatasetSchemaVersion: dataset.Manifest.SchemaVersion,
            Provider: provider.Name,
            Model: provider.Model,
            IsModelEvaluation: provider.IsModelEvaluation,
            SelectedCaseCount: selectedCases.Length,
            Summary: summary,
            Languages: reportBuilder.BuildBreakdowns(results, item => item.Language),
            Scenarios: reportBuilder.BuildBreakdowns(results, item => item.Scenario),
            Usage: new EvaluationUsage(
                Status: "unavailable",
                Reason: "ILLMClient does not expose provider token usage; no cost estimate was fabricated.",
                InputTokens: null,
                OutputTokens: null,
                EstimatedCostUsd: null),
            Gate: new EvaluationGate(
                gateFailures.Count == 0 ? "passed" : "failed",
                gateFailures),
            Cases: results);
    }

    private static IReadOnlyList<string> BuildGateFailures(
        EvaluationSummary summary,
        RunnerOptions options)
    {
        var failures = new List<string>();
        if (summary.ContractValidityRate < options.MinimumContractValidity)
        {
            failures.Add(
                $"Contract validity is {summary.ContractValidityRate}%; required minimum is {options.MinimumContractValidity}%.");
        }

        if (summary.TolerantPassRate < options.MinimumTolerantPassRate)
        {
            failures.Add(
                $"Tolerant pass rate is {summary.TolerantPassRate}%; required minimum is {options.MinimumTolerantPassRate}%.");
        }

        return failures;
    }

    private static EvaluationCase[] SelectCases(
        IReadOnlyList<EvaluationCase> cases,
        int? limit)
    {
        if (limit is null || limit.Value >= cases.Count)
        {
            return cases.ToArray();
        }

        return Enumerable.Range(0, limit.Value)
            .Select(index => cases[index * cases.Count / limit.Value])
            .ToArray();
    }

    private static string SafeErrorMessage(Exception exception)
    {
        var message = exception is OperationCanceledException
            ? "Evaluation case timed out."
            : exception.Message;
        var singleLine = message.ReplaceLineEndings(" ").Trim();
        return singleLine.Length <= 500 ? singleLine : singleLine[..500];
    }
}
