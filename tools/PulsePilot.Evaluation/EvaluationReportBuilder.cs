namespace PulsePilot.Evaluation;

public sealed class EvaluationReportBuilder
{
    public EvaluationSummary BuildSummary(
        IReadOnlyCollection<EvaluationCaseResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        if (results.Count == 0)
        {
            throw new ArgumentException("At least one evaluation result is required.", nameof(results));
        }

        var completed = results.Count(item => item.Metrics.ContractValid);
        var latencies = results
            .Select(item => item.LatencyMilliseconds)
            .Order()
            .ToArray();

        return new EvaluationSummary(
            TotalCases: results.Count,
            CompletedCases: completed,
            FailedCases: results.Count - completed,
            ContractValidityRate: Rate(completed, results.Count),
            StrictCategoryAccuracy: Rate(
                results.Count(item => item.Metrics.StrictCategoryMatch),
                results.Count),
            TolerantCategoryAccuracy: Rate(
                results.Count(item => item.Metrics.TolerantCategoryMatch),
                results.Count),
            StrictComponentAccuracy: Rate(
                results.Count(item => item.Metrics.StrictComponentMatch),
                results.Count),
            TolerantComponentAccuracy: Rate(
                results.Count(item => item.Metrics.TolerantComponentMatch),
                results.Count),
            StrictSentimentAccuracy: Rate(
                results.Count(item => item.Metrics.StrictSentimentMatch),
                results.Count),
            TolerantSentimentAccuracy: Rate(
                results.Count(item => item.Metrics.TolerantSentimentMatch),
                results.Count),
            SeverityAccuracy: Rate(
                results.Count(item => item.Metrics.SeverityWithinRange),
                results.Count),
            SummaryConceptRecall: Average(
                results.Select(item => item.Metrics.SummaryConceptRecall)),
            ActionConceptRecall: Average(
                results.Select(item => item.Metrics.ActionConceptRecall)),
            ConfidenceFloorRate: Rate(
                results.Count(item => item.Metrics.ConfidenceMeetsFloor),
                results.Count),
            StrictPassRate: Rate(
                results.Count(item => item.Metrics.StrictPass),
                results.Count),
            TolerantPassRate: Rate(
                results.Count(item => item.Metrics.TolerantPass),
                results.Count),
            Latency: new EvaluationLatencyMetrics(
                AverageMilliseconds: decimal.Round(
                    (decimal)latencies.Average(),
                    2,
                    MidpointRounding.AwayFromZero),
                P50Milliseconds: Percentile(latencies, 0.50m),
                P95Milliseconds: Percentile(latencies, 0.95m),
                MaximumMilliseconds: latencies[^1]));
    }

    public IReadOnlyList<EvaluationBreakdown> BuildBreakdowns(
        IReadOnlyCollection<EvaluationCaseResult> results,
        Func<EvaluationCaseResult, string> selector)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(selector);

        return results
            .GroupBy(selector, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var items = group.ToArray();
                return new EvaluationBreakdown(
                    group.Key,
                    items.Length,
                    items.Count(item => item.Metrics.ContractValid),
                    Rate(items.Count(item => item.Metrics.ContractValid), items.Length),
                    Rate(items.Count(item => item.Metrics.StrictPass), items.Length),
                    Rate(items.Count(item => item.Metrics.TolerantPass), items.Length));
            })
            .ToArray();
    }

    private static decimal Rate(int count, int total)
    {
        return total == 0
            ? 0m
            : decimal.Round(
                count * 100m / total,
                2,
                MidpointRounding.AwayFromZero);
    }

    private static decimal Average(IEnumerable<decimal> values)
    {
        return decimal.Round(
            values.Average() * 100m,
            2,
            MidpointRounding.AwayFromZero);
    }

    private static long Percentile(IReadOnlyList<long> sortedValues, decimal percentile)
    {
        var index = (int)Math.Ceiling(percentile * sortedValues.Count) - 1;
        return sortedValues[Math.Clamp(index, 0, sortedValues.Count - 1)];
    }
}
