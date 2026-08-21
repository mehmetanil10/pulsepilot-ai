using System.Text.Json;
using System.Text.Json.Serialization;

namespace PulsePilot.Evaluation;

public sealed class EvaluationDatasetLoader
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };

    public LoadedEvaluationDataset Load(string manifestPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);

        var absoluteManifestPath = Path.GetFullPath(manifestPath);
        var manifest = JsonSerializer.Deserialize<EvaluationManifest>(
            File.ReadAllText(absoluteManifestPath),
            SerializerOptions)
            ?? throw new InvalidDataException("Evaluation manifest is empty.");

        ValidateManifest(manifest);
        var datasetDirectory = Path.GetDirectoryName(absoluteManifestPath)
            ?? throw new InvalidDataException("Evaluation manifest has no parent directory.");
        var datasetPath = Path.GetFullPath(Path.Combine(datasetDirectory, manifest.File));

        if (!datasetPath.StartsWith(
            datasetDirectory + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Evaluation dataset must remain beside its manifest.");
        }

        var cases = File.ReadLines(datasetPath)
            .Select((line, index) => DeserializeCase(line, index + 1))
            .ToArray();

        if (cases.Length != manifest.CaseCount)
        {
            throw new InvalidDataException(
                $"Manifest declares {manifest.CaseCount} cases but dataset contains {cases.Length}.");
        }

        if (cases.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count()
            != cases.Length)
        {
            throw new InvalidDataException("Evaluation case identifiers must be unique.");
        }

        if (cases.Any(item => item.SchemaVersion != manifest.SchemaVersion))
        {
            throw new InvalidDataException(
                "Every evaluation case must use the manifest schema version.");
        }

        ValidateDistribution(cases, manifest.Languages, item => item.Language, "language");
        ValidateDistribution(cases, manifest.Scenarios, item => item.Scenario, "scenario");

        return new LoadedEvaluationDataset(absoluteManifestPath, manifest, cases);
    }

    private static EvaluationCase DeserializeCase(string line, int lineNumber)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            throw new InvalidDataException($"Evaluation line {lineNumber} is empty.");
        }

        try
        {
            return JsonSerializer.Deserialize<EvaluationCase>(line, SerializerOptions)
                ?? throw new InvalidDataException(
                    $"Evaluation line {lineNumber} is null.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Evaluation line {lineNumber} is invalid JSON.",
                exception);
        }
    }

    private static void ValidateManifest(EvaluationManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.Name)
            || string.IsNullOrWhiteSpace(manifest.Version)
            || string.IsNullOrWhiteSpace(manifest.SchemaVersion)
            || string.IsNullOrWhiteSpace(manifest.File)
            || manifest.CaseCount < 1
            || manifest.Languages is null
            || manifest.Scenarios is null
            || manifest.DataPolicy is null)
        {
            throw new InvalidDataException("Evaluation manifest is incomplete.");
        }

        if (!manifest.DataPolicy.SyntheticOnly
            || manifest.DataPolicy.RealCustomerDataAllowed
            || manifest.DataPolicy.ExternalProviderCallsRequired)
        {
            throw new InvalidDataException(
                "Evaluation manifest must enforce the synthetic, provider-free data policy.");
        }
    }

    private static void ValidateDistribution(
        IReadOnlyList<EvaluationCase> cases,
        IReadOnlyDictionary<string, int> expected,
        Func<EvaluationCase, string> selector,
        string label)
    {
        var actual = cases
            .GroupBy(selector, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        if (expected.Count != actual.Count
            || expected.Any(item => !actual.TryGetValue(item.Key, out var count)
                || count != item.Value))
        {
            throw new InvalidDataException(
                $"Evaluation {label} distribution does not match the manifest.");
        }
    }
}
