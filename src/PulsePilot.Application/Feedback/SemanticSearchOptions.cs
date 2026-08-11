namespace PulsePilot.Application.Feedback;

public sealed class SemanticSearchOptions
{
    public const string SectionName = "SemanticSearch";
    public const double DefaultSimilarityThreshold = 0.80;
    public const int DefaultResultLimit = 10;
    public const int MaximumResultLimit = 50;

    public double SimilarityThreshold { get; set; } = DefaultSimilarityThreshold;

    public int DefaultLimit { get; set; } = DefaultResultLimit;

    public int MaxLimit { get; set; } = MaximumResultLimit;
}
