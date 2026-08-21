using System.Text.RegularExpressions;
using Serilog.Core;
using Serilog.Events;

namespace PulsePilot.Infrastructure.Observability;

public sealed partial class PiiRedactionEnricher : ILogEventEnricher
{
    public const string RedactedValue = "[REDACTED]";

    private const int MaximumTraversalDepth = 8;

    private static readonly HashSet<string> SensitivePropertyNames = new(
        StringComparer.Ordinal)
    {
        "apikey",
        "authorization",
        "clientsecret",
        "content",
        "cookie",
        "customeremail",
        "customername",
        "email",
        "feedbackcontent",
        "feedbacktitle",
        "idtoken",
        "password",
        "passwd",
        "payload",
        "prompt",
        "pwd",
        "refreshtoken",
        "requestbody",
        "responsebody",
        "secret",
        "setcookie",
        "token",
        "accesstoken",
        "usermessage",
    };

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(propertyFactory);

        foreach (var property in logEvent.Properties.ToArray())
        {
            var redactedValue = IsSensitivePropertyName(property.Key)
                ? new ScalarValue(RedactedValue)
                : RedactValue(property.Value, depth: 0);

            if (!Equals(redactedValue, property.Value))
            {
                logEvent.AddOrUpdateProperty(
                    new LogEventProperty(property.Key, redactedValue));
            }
        }
    }

    private static LogEventPropertyValue RedactValue(
        LogEventPropertyValue value,
        int depth)
    {
        if (depth >= MaximumTraversalDepth)
        {
            return new ScalarValue(RedactedValue);
        }

        return value switch
        {
            ScalarValue { Value: string text } => new ScalarValue(RedactText(text)),
            SequenceValue sequence => new SequenceValue(sequence.Elements.Select(
                element => RedactValue(element, depth + 1))),
            StructureValue structure => new StructureValue(
                structure.Properties.Select(property => new LogEventProperty(
                    property.Name,
                    IsSensitivePropertyName(property.Name)
                        ? new ScalarValue(RedactedValue)
                        : RedactValue(property.Value, depth + 1))),
                structure.TypeTag),
            DictionaryValue dictionary => new DictionaryValue(dictionary.Elements.Select(
                pair => new KeyValuePair<ScalarValue, LogEventPropertyValue>(
                    pair.Key,
                    pair.Key.Value is string key && IsSensitivePropertyName(key)
                        ? new ScalarValue(RedactedValue)
                        : RedactValue(pair.Value, depth + 1)))),
            _ => value,
        };
    }

    private static string RedactText(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var redacted = BearerTokenRegex().Replace(value, $"Bearer {RedactedValue}");
        redacted = JwtRegex().Replace(redacted, RedactedValue);
        redacted = OpenAiKeyRegex().Replace(redacted, RedactedValue);
        redacted = SensitiveKeyValueRegex().Replace(
            redacted,
            match => $"{match.Groups["key"].Value}={RedactedValue}");

        return EmailRegex().Replace(redacted, RedactedValue);
    }

    private static bool IsSensitivePropertyName(string propertyName)
    {
        var normalized = new string(propertyName
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

        return SensitivePropertyNames.Contains(normalized);
    }

    [GeneratedRegex(
        @"\bBearer\s+[A-Za-z0-9\-._~+/]+=*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex BearerTokenRegex();

    [GeneratedRegex(
        @"\b[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\b",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex JwtRegex();

    [GeneratedRegex(
        @"\bsk-[A-Za-z0-9_-]{10,}\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex OpenAiKeyRegex();

    [GeneratedRegex(
        @"(?<key>password|passwd|pwd|secret|token|api[_-]?key|email|authorization|user\s*id|username)\s*=\s*[^;&\s]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex SensitiveKeyValueRegex();

    [GeneratedRegex(
        @"\b[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex EmailRegex();
}
