using PulsePilot.Evaluation;

return await EvaluationProgram.RunAsync(args);

internal static class EvaluationProgram
{
    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            var options = RunnerOptions.Parse(args);
            if (options.ShowHelp)
            {
                Console.WriteLine(RunnerOptions.HelpText);
                return 0;
            }

            var dataset = new EvaluationDatasetLoader().Load(options.ManifestPath);
            await using var provider = CreateProvider(options);
            Console.WriteLine(
                $"Dataset: {dataset.Manifest.Name} v{dataset.Manifest.Version} ({dataset.Manifest.CaseCount} cases)");
            Console.WriteLine(
                $"Provider: {provider.Name} / {provider.Model} / modelEvaluation={provider.IsModelEvaluation}");
            Console.WriteLine();

            var runner = new EvaluationRunner(
                new EvaluationScorer(),
                new EvaluationReportBuilder());
            var report = await runner.RunAsync(
                dataset,
                provider,
                options,
                Console.Out,
                CancellationToken.None);
            await new EvaluationReportWriter().WriteAsync(report, options.OutputPath);
            WriteSummary(report, options.OutputPath);

            return report.Gate.Status == "passed" ? 0 : 1;
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine($"Configuration error: {exception.Message}");
            Console.Error.WriteLine();
            Console.Error.WriteLine(RunnerOptions.HelpText);
            return 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                $"Evaluation failed: {exception.GetType().Name}: {exception.Message}");
            return 1;
        }
    }

    private static IEvaluationProvider CreateProvider(RunnerOptions options)
    {
        return options.Provider switch
        {
            "replay" => new ReplayEvaluationProvider(),
            "openai" => OpenAiEvaluationProvider.Create(options),
            _ => throw new InvalidOperationException(
                $"Unsupported provider '{options.Provider}'."),
        };
    }

    private static void WriteSummary(EvaluationReport report, string outputPath)
    {
        Console.WriteLine();
        Console.WriteLine("PulsePilot evaluation summary");
        Console.WriteLine(
            $"  Contract validity:  {report.Summary.ContractValidityRate}%");
        Console.WriteLine(
            $"  Category accuracy:  {report.Summary.StrictCategoryAccuracy}% strict / {report.Summary.TolerantCategoryAccuracy}% tolerant");
        Console.WriteLine(
            $"  Component accuracy: {report.Summary.StrictComponentAccuracy}% strict / {report.Summary.TolerantComponentAccuracy}% tolerant");
        Console.WriteLine(
            $"  Sentiment accuracy: {report.Summary.StrictSentimentAccuracy}% strict / {report.Summary.TolerantSentimentAccuracy}% tolerant");
        Console.WriteLine(
            $"  Severity accuracy:  {report.Summary.SeverityAccuracy}%");
        Console.WriteLine(
            $"  Concept recall:     {report.Summary.SummaryConceptRecall}% summary / {report.Summary.ActionConceptRecall}% action");
        Console.WriteLine(
            $"  Full pass rate:     {report.Summary.StrictPassRate}% strict / {report.Summary.TolerantPassRate}% tolerant");
        Console.WriteLine(
            $"  Latency p50/p95:    {report.Summary.Latency.P50Milliseconds} ms / {report.Summary.Latency.P95Milliseconds} ms");
        Console.WriteLine($"  Gate:               {report.Gate.Status}");
        Console.WriteLine($"  Report:             {Path.GetFullPath(outputPath)}");

        if (!report.IsModelEvaluation)
        {
            Console.WriteLine(
                "  Note: replay validates mechanics only; these are not model-quality scores.");
        }

        foreach (var failure in report.Gate.Failures)
        {
            Console.WriteLine($"  - {failure}");
        }
    }
}
