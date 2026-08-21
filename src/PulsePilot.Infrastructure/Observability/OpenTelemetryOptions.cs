namespace PulsePilot.Infrastructure.Observability;

public sealed class OpenTelemetryOptions
{
    public const string SectionName = "OpenTelemetry";

    public bool OtlpEnabled { get; init; }

    public Uri OtlpEndpoint { get; init; } = new("http://localhost:4317");
}
