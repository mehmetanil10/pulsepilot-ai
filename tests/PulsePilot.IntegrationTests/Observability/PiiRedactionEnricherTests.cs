using PulsePilot.Infrastructure.Observability;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace PulsePilot.IntegrationTests.Observability;

public sealed class PiiRedactionEnricherTests
{
    [Fact]
    public void Enricher_RedactsSensitiveNamesAndNestedTextPatterns()
    {
        var sink = new CapturingSink();
        using var logger = new LoggerConfiguration()
            .Enrich.With<PiiRedactionEnricher>()
            .WriteTo.Sink(sink)
            .CreateLogger();
        const string email = "customer.private@example.com";
        const string bearerToken = "Bearer abcdefghijklmnopqrstuvwxyz.abcdefghijklmno";
        const string jwt =
            "abcdefghijk.abcdefghijkl.abcdefghijklm";
        const string apiKey = "sk-super-secret-provider-key";

        logger.Information(
            "Login {Email} {Authorization} {@Profile} at {RequestPath}",
            email,
            bearerToken,
            new
            {
                CustomerName = "Private Customer",
                Note = $"Contact {email}; token={jwt}; api_key={apiKey}",
                Metadata = new Dictionary<string, string>
                {
                    ["password"] = "customer-password",
                    ["region"] = "eu-central",
                },
            },
            $"/api/feedback?email={email}&token={jwt}");

        var logEvent = Assert.Single(sink.Events);
        var serialized = logEvent.RenderMessage()
            + string.Join(
                ' ',
                logEvent.Properties.Select(property => property.Value.ToString()));

        Assert.DoesNotContain(email, serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Private Customer", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("customer-password", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(jwt, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(apiKey, serialized, StringComparison.Ordinal);
        Assert.Contains(PiiRedactionEnricher.RedactedValue, serialized, StringComparison.Ordinal);
        Assert.Contains("eu-central", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public void Enricher_PreservesOperationalMetadata()
    {
        var sink = new CapturingSink();
        using var logger = new LoggerConfiguration()
            .Enrich.With<PiiRedactionEnricher>()
            .WriteTo.Sink(sink)
            .CreateLogger();
        var feedbackId = Guid.CreateVersion7();
        var workspaceId = Guid.CreateVersion7();

        logger.Information(
            "Feedback {FeedbackId} in {WorkspaceId} completed after {Attempts} attempts",
            feedbackId,
            workspaceId,
            3);

        var logEvent = Assert.Single(sink.Events);

        Assert.Equal(feedbackId, GetScalarValue<Guid>(logEvent, "FeedbackId"));
        Assert.Equal(workspaceId, GetScalarValue<Guid>(logEvent, "WorkspaceId"));
        Assert.Equal(3, GetScalarValue<int>(logEvent, "Attempts"));
    }

    private static T GetScalarValue<T>(LogEvent logEvent, string propertyName)
    {
        var value = Assert.IsType<ScalarValue>(logEvent.Properties[propertyName]);
        return Assert.IsType<T>(value.Value);
    }

    private sealed class CapturingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent)
        {
            Events.Add(logEvent);
        }
    }
}
