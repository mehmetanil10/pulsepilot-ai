namespace PulsePilot.Application.FeedbackClusters;

public sealed class FeedbackClusterQuery
{
    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}
