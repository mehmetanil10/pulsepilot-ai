using System.Text.Json;
using System.Text.RegularExpressions;
using PulsePilot.Domain.Feedback;

namespace PulsePilot.UnitTests.Evaluation;

public sealed class FeedbackAnalysisDatasetTests
{
    private const int ExpectedCaseCount = 60;
    private const string ExpectedSchemaVersion = "1.0";
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    [Fact]
    public void Dataset_IsWellFormedUniqueAndAlignedWithProductContract()
    {
        var cases = LoadCases();

        Assert.Equal(ExpectedCaseCount, cases.Count);
        Assert.Equal(cases.Count, cases.Select(item => item.Id).Distinct().Count());

        foreach (var item in cases)
        {
            Assert.Equal(ExpectedSchemaVersion, item.SchemaVersion);
            Assert.Matches("^fa-(en|tr)-[a-z0-9-]+-[0-9]{3}$", item.Id);
            Assert.Contains(item.Language, new[] { "en", "tr" });
            Assert.Contains(item.Scenario, ExpectedScenarios);
            Assert.True(item.Input.Title is null
                || item.Input.Title.Length <= Feedback.MaxTitleLength);
            Assert.False(string.IsNullOrWhiteSpace(item.Input.Content));
            Assert.True(item.Input.Content.Length <= Feedback.MaxContentLength);
            AssertEnumValue<FeedbackSource>(item.Input.Source);
            AssertExpectation<FeedbackCategory>(item.Expected.Category);
            AssertExpectation<FeedbackComponent>(item.Expected.Component);
            AssertExpectation<FeedbackSentiment>(item.Expected.Sentiment);
            Assert.InRange(
                item.Expected.Severity.Minimum,
                FeedbackAnalysis.MinimumSeverity,
                FeedbackAnalysis.MaximumSeverity);
            Assert.InRange(
                item.Expected.Severity.Maximum,
                FeedbackAnalysis.MinimumSeverity,
                FeedbackAnalysis.MaximumSeverity);
            Assert.True(item.Expected.Severity.Minimum <= item.Expected.Severity.Maximum);
            Assert.InRange(item.Expected.MinimumConfidence, 0m, 1m);
            AssertConcepts(item.Expected.RequiredSummaryConcepts);
            AssertConcepts(item.Expected.RequiredActionConcepts);
            Assert.NotEmpty(item.Tags);
            Assert.Equal(item.Tags.Length, item.Tags.Distinct().Count());
            Assert.All(item.Tags, tag => Assert.Matches("^[a-z0-9-]+$", tag));
        }
    }

    [Fact]
    public void Dataset_HasDeclaredBalanceBreadthAndSafetyCoverage()
    {
        var cases = LoadCases();
        using var manifest = LoadJson("datasets", "feedback-analysis.v1.manifest.json");
        var root = manifest.RootElement;

        Assert.Equal(ExpectedCaseCount, root.GetProperty("caseCount").GetInt32());
        Assert.Equal(ExpectedSchemaVersion, root.GetProperty("schemaVersion").GetString());
        Assert.True(root.GetProperty("dataPolicy").GetProperty("syntheticOnly").GetBoolean());
        Assert.False(root.GetProperty("dataPolicy").GetProperty("realCustomerDataAllowed").GetBoolean());
        Assert.False(root.GetProperty("dataPolicy").GetProperty("externalProviderCallsRequired").GetBoolean());
        AssertDistribution(cases, root.GetProperty("languages"), item => item.Language);
        AssertDistribution(cases, root.GetProperty("scenarios"), item => item.Scenario);

        AssertEnumCoverage<FeedbackSource>(cases.Select(item => item.Input.Source));
        AssertEnumCoverage<FeedbackCategory>(cases.Select(item => item.Expected.Category.Preferred));
        AssertEnumCoverage<FeedbackComponent>(cases.Select(item => item.Expected.Component.Preferred));
        AssertEnumCoverage<FeedbackSentiment>(cases.Select(item => item.Expected.Sentiment.Preferred));
        Assert.Equal(6, cases.Count(item => item.Tags.Contains("prompt-injection")));
        Assert.Equal(4, cases.Count(item => item.Tags.Contains("synthetic-pii")));
        Assert.All(
            cases.Where(item => item.Tags.Contains("synthetic-pii")),
            item => Assert.Contains("example.com", item.Input.Content));
    }

    [Fact]
    public void JsonSchema_DeclaresClosedVersionedEvaluationContract()
    {
        using var schema = LoadJson("schemas", "feedback-analysis-case.schema.json");
        var root = schema.RootElement;

        Assert.Equal(
            "https://json-schema.org/draft/2020-12/schema",
            root.GetProperty("$schema").GetString());
        Assert.False(root.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            ExpectedSchemaVersion,
            root.GetProperty("properties")
                .GetProperty("schemaVersion")
                .GetProperty("const")
                .GetString());
        Assert.True(root.GetProperty("properties").GetProperty("input")
            .GetProperty("additionalProperties").ValueKind == JsonValueKind.False);
        Assert.True(root.GetProperty("properties").GetProperty("expected")
            .GetProperty("additionalProperties").ValueKind == JsonValueKind.False);

        var properties = root.GetProperty("properties");
        var inputProperties = properties.GetProperty("input").GetProperty("properties");
        var expectedProperties = properties.GetProperty("expected").GetProperty("properties");
        var definitions = root.GetProperty("$defs");
        Assert.Equal(
            Feedback.MaxTitleLength,
            inputProperties.GetProperty("title").GetProperty("maxLength").GetInt32());
        Assert.Equal(
            Feedback.MaxContentLength,
            inputProperties.GetProperty("content").GetProperty("maxLength").GetInt32());
        AssertSchemaEnum<FeedbackSource>(inputProperties.GetProperty("source").GetProperty("enum"));
        AssertSchemaEnum<FeedbackCategory>(definitions.GetProperty("categoryExpectation")
            .GetProperty("properties").GetProperty("preferred").GetProperty("enum"));
        AssertSchemaEnum<FeedbackComponent>(definitions.GetProperty("componentExpectation")
            .GetProperty("properties").GetProperty("preferred").GetProperty("enum"));
        AssertSchemaEnum<FeedbackSentiment>(definitions.GetProperty("sentimentExpectation")
            .GetProperty("properties").GetProperty("preferred").GetProperty("enum"));
        var severityProperties = expectedProperties.GetProperty("severity")
            .GetProperty("properties");
        Assert.Equal(
            FeedbackAnalysis.MinimumSeverity,
            severityProperties.GetProperty("minimum").GetProperty("minimum").GetInt32());
        Assert.Equal(
            FeedbackAnalysis.MaximumSeverity,
            severityProperties.GetProperty("maximum").GetProperty("maximum").GetInt32());
    }

    private static IReadOnlyList<EvaluationCase> LoadCases()
    {
        var path = EvaluationPath("datasets", "feedback-analysis.v1.jsonl");
        var cases = new List<EvaluationCase>();

        foreach (var line in File.ReadLines(path))
        {
            Assert.False(string.IsNullOrWhiteSpace(line));
            var item = JsonSerializer.Deserialize<EvaluationCase>(line, SerializerOptions);
            Assert.NotNull(item);
            cases.Add(item);
        }

        return cases;
    }

    private static JsonDocument LoadJson(params string[] segments)
    {
        return JsonDocument.Parse(File.ReadAllText(EvaluationPath(segments)));
    }

    private static string EvaluationPath(params string[] segments)
    {
        return Path.Combine(new[] { AppContext.BaseDirectory, "Evaluation" }
            .Concat(segments)
            .ToArray());
    }

    private static void AssertExpectation<TEnum>(CategoricalExpectation expectation)
        where TEnum : struct, Enum
    {
        Assert.NotEmpty(expectation.Accepted);
        Assert.Contains(expectation.Preferred, expectation.Accepted);
        Assert.Equal(expectation.Accepted.Length, expectation.Accepted.Distinct().Count());
        AssertEnumValue<TEnum>(expectation.Preferred);
        Assert.All(expectation.Accepted, AssertEnumValue<TEnum>);
    }

    private static void AssertEnumValue<TEnum>(string value)
        where TEnum : struct, Enum
    {
        Assert.True(Enum.TryParse<TEnum>(value, ignoreCase: false, out var parsed));
        Assert.True(Enum.IsDefined(parsed));
    }

    private static void AssertConcepts(string[] concepts)
    {
        Assert.InRange(concepts.Length, 1, 8);
        Assert.Equal(concepts.Length, concepts.Distinct().Count());
        Assert.All(concepts, concept =>
        {
            Assert.Equal(concept.Trim(), concept);
            Assert.Equal(concept.ToLowerInvariant(), concept);
            Assert.InRange(concept.Length, 2, 80);
        });
    }

    private static void AssertDistribution(
        IReadOnlyList<EvaluationCase> cases,
        JsonElement expectedDistribution,
        Func<EvaluationCase, string> selector)
    {
        foreach (var property in expectedDistribution.EnumerateObject())
        {
            Assert.Equal(
                property.Value.GetInt32(),
                cases.Count(item => selector(item) == property.Name));
        }
    }

    private static void AssertEnumCoverage<TEnum>(IEnumerable<string> values)
        where TEnum : struct, Enum
    {
        Assert.Equal(
            Enum.GetNames<TEnum>().Order(),
            values.Distinct().Order());
    }

    private static void AssertSchemaEnum<TEnum>(JsonElement values)
        where TEnum : struct, Enum
    {
        Assert.Equal(
            Enum.GetNames<TEnum>().Order(),
            values.EnumerateArray().Select(value => value.GetString()).Order());
    }

    private static readonly string[] ExpectedScenarios =
    [
        "clear_signal",
        "mixed_signal",
        "ambiguous",
        "noisy",
        "adversarial",
        "synthetic_pii",
        "minimal",
    ];

    private sealed record EvaluationCase(
        string SchemaVersion,
        string Id,
        string Language,
        string Scenario,
        EvaluationInput Input,
        EvaluationExpectation Expected,
        string[] Tags);

    private sealed record EvaluationInput(
        string? Title,
        string Content,
        string Source);

    private sealed record EvaluationExpectation(
        CategoricalExpectation Category,
        CategoricalExpectation Component,
        SeverityExpectation Severity,
        CategoricalExpectation Sentiment,
        string[] RequiredSummaryConcepts,
        string[] RequiredActionConcepts,
        decimal MinimumConfidence);

    private sealed record CategoricalExpectation(
        string Preferred,
        string[] Accepted);

    private sealed record SeverityExpectation(int Minimum, int Maximum);
}
