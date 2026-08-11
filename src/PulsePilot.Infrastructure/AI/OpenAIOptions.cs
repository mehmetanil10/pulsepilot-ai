namespace PulsePilot.Infrastructure.AI;

public sealed class OpenAIOptions
{
    public const string SectionName = "OpenAI";
    public const string DefaultModel = "gpt-5.6-luna";
    public const string DefaultEmbeddingModel = "text-embedding-3-small";
    public const int DefaultMaxOutputTokenCount = 1_000;
    public const int DefaultNetworkTimeoutSeconds = 30;

    public bool Enabled { get; set; }

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = DefaultModel;

    public string EmbeddingModel { get; set; } = DefaultEmbeddingModel;

    public int EmbeddingDimensions { get; set; } = 1_536;

    public Uri Endpoint { get; set; } = new("https://api.openai.com/v1/");

    public int MaxOutputTokenCount { get; set; } = DefaultMaxOutputTokenCount;

    public int NetworkTimeoutSeconds { get; set; } = DefaultNetworkTimeoutSeconds;
}
