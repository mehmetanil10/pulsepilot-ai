using System.Text.Json;
using System.Text.Json.Serialization;

namespace PulsePilot.Evaluation;

public sealed class EvaluationReportWriter
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        };

    public async Task WriteAsync(
        EvaluationReport report,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        var absolutePath = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(absolutePath)
            ?? throw new ArgumentException("Output path requires a parent directory.", nameof(outputPath));
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(
            absolutePath,
            JsonSerializer.Serialize(report, SerializerOptions),
            cancellationToken);
    }
}
