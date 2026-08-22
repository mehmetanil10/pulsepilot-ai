using PulsePilot.Application;
using PulsePilot.Infrastructure;
using PulsePilot.Infrastructure.FeedbackProcessing;
using PulsePilot.Infrastructure.Observability;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.With<PiiRedactionEnricher>()
    .Enrich.FromLogContext());

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddPulsePilotOpenTelemetry(
    builder.Configuration,
    serviceName: "PulsePilot.Worker");
builder.Services.AddFeedbackAnalysisWorker(builder.Configuration);

await builder.Build().RunAsync();
