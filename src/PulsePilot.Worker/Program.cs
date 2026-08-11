using PulsePilot.Application;
using PulsePilot.Application.FeedbackProcessing;
using PulsePilot.Infrastructure;
using PulsePilot.Worker;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOptions<FeedbackProcessingOptions>()
    .BindConfiguration(FeedbackProcessingOptions.SectionName)
    .Validate(
        options => options.PollIntervalMilliseconds is >= 100 and <= 60_000,
        "Feedback processing poll interval must be between 100 and 60000 milliseconds.")
    .Validate(
        options => options.RecoveryIntervalSeconds is >= 10 and <= 3_600,
        "Feedback processing recovery interval must be between 10 and 3600 seconds.")
    .Validate(
        options => options.StaleProcessingThresholdMinutes is >= 1 and <= 1_440,
        "Stale processing threshold must be between 1 and 1440 minutes.")
    .Validate(
        options => options.MaxAttempts is >= 1 and <= 5,
        "Feedback processing max attempts must be between 1 and 5.")
    .Validate(
        options => options.AnalysisTimeoutSeconds is >= 5 and <= 300,
        "Feedback analysis timeout must be between 5 and 300 seconds.")
    .Validate(
        options => options.BaseRetryDelayMilliseconds is >= 0 and <= 60_000,
        "Feedback processing base retry delay must be between 0 and 60000 milliseconds.")
    .Validate(
        options => options.MaxRetryDelaySeconds is >= 1 and <= 300,
        "Feedback processing max retry delay must be between 1 and 300 seconds.")
    .Validate(
        options => options.RetryJitterFactor is >= 0 and <= 1,
        "Feedback processing retry jitter factor must be between 0 and 1.")
    .Validate(
        options => options.MaxRecoveredPerSweep is >= 1 and <= 1_000,
        "Feedback processing recovery batch size must be between 1 and 1000.")
    .Validate(
        options => options.StaleProcessingThresholdMinutes * 60
            > options.AnalysisTimeoutSeconds * options.MaxAttempts
                + options.MaxRetryDelaySeconds * (options.MaxAttempts - 1),
        "Stale processing threshold must exceed the maximum analysis and retry window.")
    .Validate(
        options => !options.Enabled
            || builder.Configuration.GetValue<bool>("OpenAI:Enabled"),
        "OpenAI must be enabled when feedback processing is enabled.")
    .ValidateOnStart();
builder.Services.AddHostedService<FeedbackAnalysisWorker>();

await builder.Build().RunAsync();
