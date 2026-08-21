using System.Text.Json;
using PulsePilot.Application.AI;
using PulsePilot.Evaluation;

namespace PulsePilot.UnitTests.Evaluation;

public sealed class EvaluationRunnerTests
{
    [Fact]
    public async Task RunAsync_ReplayProcessesFullManifestWithoutClaimingModelQuality()
    {
        var options = RunnerOptions.Parse([]);
        var dataset = new EvaluationDatasetLoader().Load(options.ManifestPath);
        await using var provider = new ReplayEvaluationProvider();
        var runner = CreateRunner();

        var report = await runner.RunAsync(
            dataset,
            provider,
            options,
            TextWriter.Null);

        Assert.Equal(60, report.SelectedCaseCount);
        Assert.Equal(100m, report.Summary.ContractValidityRate);
        Assert.Equal(100m, report.Summary.StrictPassRate);
        Assert.Equal(100m, report.Summary.TolerantPassRate);
        Assert.False(report.IsModelEvaluation);
        Assert.Equal("unavailable", report.Usage.Status);
        Assert.Equal("passed", report.Gate.Status);
    }

    [Fact]
    public async Task RunAsync_ProviderFailureIsCapturedAndFailsContractGate()
    {
        var options = RunnerOptions.Parse(["--limit", "1"]);
        var dataset = new EvaluationDatasetLoader().Load(options.ManifestPath);
        await using var provider = new ThrowingProvider();

        var report = await CreateRunner().RunAsync(
            dataset,
            provider,
            options,
            TextWriter.Null);

        var result = Assert.Single(report.Cases);
        Assert.Equal(0m, report.Summary.ContractValidityRate);
        Assert.Equal("failed", report.Gate.Status);
        Assert.NotEmpty(report.Gate.Failures);
        Assert.NotNull(result.Error);
        Assert.Null(result.Actual);
    }

    [Fact]
    public void Parse_AcceptsBoundedProviderAndGateOptions()
    {
        var options = RunnerOptions.Parse(
        [
            "--provider", "openai",
            "--model", "test-model",
            "--endpoint", "https://example.com/v1/",
            "--limit", "7",
            "--case-timeout-seconds", "45",
            "--minimum-contract-validity", "95.5",
            "--minimum-tolerant-pass-rate", "70",
        ]);

        Assert.Equal("openai", options.Provider);
        Assert.Equal("test-model", options.Model);
        Assert.Equal(new Uri("https://example.com/v1/"), options.Endpoint);
        Assert.Equal(7, options.Limit);
        Assert.Equal(45, options.CaseTimeoutSeconds);
        Assert.Equal(95.5m, options.MinimumContractValidity);
        Assert.Equal(70m, options.MinimumTolerantPassRate);
    }

    [Fact]
    public void Parse_RejectsInsecureProviderEndpoint()
    {
        var exception = Assert.Throws<ArgumentException>(() => RunnerOptions.Parse(
            ["--endpoint", "http://example.com/v1/"]));

        Assert.Contains("HTTPS", exception.Message);
    }

    [Fact]
    public async Task WriteAsync_SerializesEnumsAsStringsAndKeepsUsageUnavailable()
    {
        var options = RunnerOptions.Parse(["--limit", "1"]);
        var dataset = new EvaluationDatasetLoader().Load(options.ManifestPath);
        await using var provider = new ReplayEvaluationProvider();
        var report = await CreateRunner().RunAsync(
            dataset,
            provider,
            options,
            TextWriter.Null);
        var directory = Path.Combine(Path.GetTempPath(), $"pulsepilot-eval-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "report.json");

        try
        {
            await new EvaluationReportWriter().WriteAsync(report, path);
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path));
            var root = document.RootElement;

            Assert.Equal("replay", root.GetProperty("provider").GetString());
            Assert.Equal(
                "Bug",
                root.GetProperty("cases")[0]
                    .GetProperty("actual")
                    .GetProperty("category")
                    .GetString());
            Assert.Equal(
                JsonValueKind.Null,
                root.GetProperty("usage").GetProperty("estimatedCostUsd").ValueKind);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static EvaluationRunner CreateRunner()
    {
        return new EvaluationRunner(
            new EvaluationScorer(),
            new EvaluationReportBuilder());
    }

    private sealed class ThrowingProvider : IEvaluationProvider
    {
        public string Name => "throwing";

        public string Model => "test";

        public bool IsModelEvaluation => false;

        public Task<FeedbackAnalysisResult> AnalyzeAsync(
            EvaluationCase evaluationCase,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Synthetic provider failure.");
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
