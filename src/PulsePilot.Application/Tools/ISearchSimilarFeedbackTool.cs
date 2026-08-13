using PulsePilot.Domain.Feedback;

namespace PulsePilot.Application.Tools;

public interface ISearchSimilarFeedbackTool
{
    Task<SearchSimilarFeedbackToolResult> ExecuteAsync(
        Guid workspaceId,
        SearchSimilarFeedbackToolInput input,
        CancellationToken cancellationToken = default);
}

public sealed record SearchSimilarFeedbackToolInput(
    Guid FeedbackId,
    int? Limit = null);

public sealed record SearchSimilarFeedbackToolResult(
    Guid FeedbackId,
    double SimilarityThreshold,
    IReadOnlyList<SearchSimilarFeedbackToolItem> Items)
{
    public int Count => Items.Count;
}

public sealed record SearchSimilarFeedbackToolItem(
    Guid FeedbackId,
    Guid? FeedbackClusterId,
    string? Title,
    string Content,
    FeedbackSource Source,
    double Similarity,
    DateTimeOffset CreatedAt);
