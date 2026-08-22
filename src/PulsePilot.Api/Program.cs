using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi;
using PulsePilot.Api.Authentication;
using PulsePilot.Api.Database;
using PulsePilot.Api.ErrorHandling;
using PulsePilot.Api.HealthChecks;
using PulsePilot.Api.Protection;
using PulsePilot.Api.Validation;
using PulsePilot.Application;
using PulsePilot.Infrastructure;
using PulsePilot.Infrastructure.FeedbackProcessing;
using PulsePilot.Infrastructure.Observability;
using PulsePilot.Infrastructure.Persistence;
using PulsePilot.Infrastructure.Persistence.Seeding;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog((services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.With<PiiRedactionEnricher>()
    .Enrich.FromLogContext());

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddFeedbackAnalysisWorker(builder.Configuration);
builder.Services.AddPulsePilotOpenTelemetry(
    builder.Configuration,
    serviceName: "PulsePilot.Api",
    instrumentAspNetCore: true);

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = ApiProblemDetailsFactory.Customize;
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddJwtAuthentication();
builder.Services.AddApiProtection(builder.Configuration);

builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddScoped<FluentValidationActionFilter>();
builder.Services.AddControllers(options =>
    options.Filters.AddService<FluentValidationActionFilter>())
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(
        new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false)));
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problemDetails = ApiProblemDetailsFactory.CreateModelBindingValidation(
            context.HttpContext,
            context.ModelState.Keys);
        var result = new BadRequestObjectResult(problemDetails);
        result.ContentTypes.Add("application/problem+json");

        return result;
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PulsePilot AI API",
        Version = "v1",
        Description = "Product feedback and engineering copilot API.",
    });

    options.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter a JWT bearer token.",
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("bearer", document)] = [],
    });
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>(
        "postgresql",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"]);

var app = builder.Build();

var runMigrations = app.Configuration.GetValue<bool>("Database:RunMigrations");
var runDemoSeed = app.Configuration.GetValue<bool>($"{DemoSeedOptions.SectionName}:Run");
var exitAfterInitialization = app.Configuration.GetValue(
    "Database:ExitAfterInitialization",
    true);

if (runMigrations || runDemoSeed)
{
    await app.ApplyDatabaseMigrationsAsync();

    if (runDemoSeed)
    {
        await app.SeedDemoDataAsync();
    }

    if (exitAfterInitialization)
    {
        await app.DisposeAsync();
        return;
    }
}

app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseApiSecurityHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "PulsePilot AI API v1"));
}

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = HealthCheckResponseWriter.WriteAsync,
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteAsync,
}).AllowAnonymous();
app.MapControllers();

app.Run();

public partial class Program;
