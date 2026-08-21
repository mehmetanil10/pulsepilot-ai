using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using PulsePilot.Infrastructure.Observability;

namespace PulsePilot.IntegrationTests.Observability;

public sealed class OpenTelemetryRegistrationTests
{
    [Fact]
    public void AddPulsePilotOpenTelemetry_RegistersTraceAndMetricProviders()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["OpenTelemetry:OtlpEnabled"] = "false",
        });

        services.AddPulsePilotOpenTelemetry(configuration, "PulsePilot.Test");
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<TracerProvider>());
        Assert.NotNull(provider.GetService<MeterProvider>());
        Assert.False(provider.GetRequiredService<IOptions<OpenTelemetryOptions>>()
            .Value
            .OtlpEnabled);
    }

    [Fact]
    public void AddPulsePilotOpenTelemetry_RejectsInvalidEnabledOtlpEndpoint()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["OpenTelemetry:OtlpEnabled"] = "true",
            ["OpenTelemetry:OtlpEndpoint"] = "relative-endpoint",
        });

        services.AddPulsePilotOpenTelemetry(configuration, "PulsePilot.Test");
        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<OpenTelemetryOptions>>().Value);
        Assert.Contains("absolute HTTP or HTTPS URI", exception.Message);
    }

    private static IConfiguration CreateConfiguration(
        IReadOnlyDictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
