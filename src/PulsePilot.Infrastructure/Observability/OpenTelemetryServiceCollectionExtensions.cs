using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PulsePilot.Application.Observability;

namespace PulsePilot.Infrastructure.Observability;

public static class OpenTelemetryServiceCollectionExtensions
{
    public static IServiceCollection AddPulsePilotOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        bool instrumentAspNetCore = false)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        var section = configuration.GetSection(OpenTelemetryOptions.SectionName);
        services.AddOptions<OpenTelemetryOptions>()
            .Bind(section)
            .Validate(
                options => !options.OtlpEnabled
                    || options.OtlpEndpoint is { IsAbsoluteUri: true }
                        && (options.OtlpEndpoint.Scheme == Uri.UriSchemeHttp
                            || options.OtlpEndpoint.Scheme == Uri.UriSchemeHttps),
                "OpenTelemetry OTLP endpoint must be an absolute HTTP or HTTPS URI when export is enabled.")
            .ValidateOnStart();

        var configuredOptions = section.Get<OpenTelemetryOptions>() ?? new OpenTelemetryOptions();
        var serviceVersion = Assembly
            .GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        var openTelemetry = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                serviceName,
                serviceVersion: serviceVersion))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(PulsePilotTelemetry.ActivitySourceName)
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                    })
                    .AddNpgsql();

                if (instrumentAspNetCore)
                {
                    tracing.AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.Filter = context => !context.Request.Path.StartsWithSegments(
                            "/health/live",
                            StringComparison.OrdinalIgnoreCase);
                    });
                }

                if (configuredOptions.OtlpEnabled)
                {
                    tracing.AddOtlpExporter(options => ConfigureExporter(
                        options,
                        configuredOptions.OtlpEndpoint));
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(PulsePilotTelemetry.MeterName)
                    .AddNpgsqlInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();

                if (instrumentAspNetCore)
                {
                    metrics.AddAspNetCoreInstrumentation();
                }

                if (configuredOptions.OtlpEnabled)
                {
                    metrics.AddOtlpExporter(options => ConfigureExporter(
                        options,
                        configuredOptions.OtlpEndpoint));
                }
            });

        _ = openTelemetry;

        return services;
    }

    private static void ConfigureExporter(OtlpExporterOptions options, Uri endpoint)
    {
        options.Endpoint = endpoint;
        options.Protocol = OtlpExportProtocol.Grpc;
    }
}
