using PulsePilot.Infrastructure.AI;

namespace PulsePilot.Evaluation;

public sealed record RunnerOptions(
    string Provider,
    string ManifestPath,
    string OutputPath,
    string Model,
    Uri Endpoint,
    int? Limit,
    int CaseTimeoutSeconds,
    decimal MinimumContractValidity,
    decimal MinimumTolerantPassRate,
    bool ShowHelp)
{
    public static RunnerOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var provider = "replay";
        var manifestPath = Path.Combine(
            AppContext.BaseDirectory,
            "Evaluation",
            "datasets",
            "feedback-analysis.v1.manifest.json");
        var outputPath = Path.Combine(
            Environment.CurrentDirectory,
            "artifacts",
            "evaluation",
            "latest.json");
        var model = Environment.GetEnvironmentVariable("OPENAI_MODEL")
            ?? OpenAIOptions.DefaultModel;
        var endpoint = new Uri(
            Environment.GetEnvironmentVariable("OPENAI_ENDPOINT")
                ?? "https://api.openai.com/v1/");
        int? limit = null;
        var caseTimeoutSeconds = OpenAIOptions.DefaultNetworkTimeoutSeconds;
        var minimumContractValidity = 100m;
        var minimumTolerantPassRate = 0m;
        var showHelp = false;

        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            if (argument is "--help" or "-h")
            {
                showHelp = true;
                continue;
            }

            var value = ReadValue(args, ref index, argument);
            switch (argument)
            {
                case "--provider":
                    provider = value.ToLowerInvariant();
                    break;
                case "--manifest":
                    manifestPath = value;
                    break;
                case "--output":
                    outputPath = value;
                    break;
                case "--model":
                    model = value;
                    break;
                case "--endpoint":
                    endpoint = ParseHttpsEndpoint(value);
                    break;
                case "--limit":
                    limit = ParseInteger(value, argument, 1, 10_000);
                    break;
                case "--case-timeout-seconds":
                    caseTimeoutSeconds = ParseInteger(value, argument, 1, 300);
                    break;
                case "--minimum-contract-validity":
                    minimumContractValidity = ParsePercentage(value, argument);
                    break;
                case "--minimum-tolerant-pass-rate":
                    minimumTolerantPassRate = ParsePercentage(value, argument);
                    break;
                default:
                    throw new ArgumentException($"Unknown evaluation option '{argument}'.");
            }
        }

        if (provider is not ("replay" or "openai"))
        {
            throw new ArgumentException("Provider must be either 'replay' or 'openai'.");
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new ArgumentException("Model cannot be empty.");
        }

        return new RunnerOptions(
            provider,
            Path.GetFullPath(manifestPath),
            Path.GetFullPath(outputPath),
            model,
            endpoint,
            limit,
            caseTimeoutSeconds,
            minimumContractValidity,
            minimumTolerantPassRate,
            showHelp);
    }

    public static string HelpText => """
        PulsePilot AI feedback evaluation runner

        Usage:
          dotnet run --project tools/PulsePilot.Evaluation -- [options]

        Options:
          --provider replay|openai           Provider mode (default: replay)
          --manifest <path>                  Versioned dataset manifest
          --output <path>                    JSON report path
          --model <name>                     OpenAI model name
          --endpoint <https-url>             OpenAI-compatible HTTPS endpoint
          --limit <count>                    Evaluate only the first N cases
          --case-timeout-seconds <1-300>     Per-case timeout
          --minimum-contract-validity <0-100>
          --minimum-tolerant-pass-rate <0-100>
          --help                             Show this help

        Real provider mode reads the API key only from OPENAI_API_KEY.
        Replay mode validates the runner and scoring pipeline; it is not a model-quality result.
        """;

    private static string ReadValue(
        IReadOnlyList<string> args,
        ref int index,
        string argument)
    {
        if (!argument.StartsWith("--", StringComparison.Ordinal)
            || index + 1 >= args.Count
            || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Option '{argument}' requires a value.");
        }

        index++;
        return args[index];
    }

    private static int ParseInteger(
        string value,
        string argument,
        int minimum,
        int maximum)
    {
        if (!int.TryParse(value, out var parsed)
            || parsed < minimum
            || parsed > maximum)
        {
            throw new ArgumentException(
                $"Option '{argument}' must be between {minimum} and {maximum}.");
        }

        return parsed;
    }

    private static decimal ParsePercentage(string value, string argument)
    {
        if (!decimal.TryParse(
                value,
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsed)
            || parsed is < 0 or > 100)
        {
            throw new ArgumentException(
                $"Option '{argument}' must be a percentage between 0 and 100.");
        }

        return parsed;
    }

    private static Uri ParseHttpsEndpoint(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var endpoint)
            || endpoint.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Endpoint must be an absolute HTTPS URI.");
        }

        return endpoint;
    }
}
