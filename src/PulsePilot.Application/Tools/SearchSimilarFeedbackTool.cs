using Microsoft.Extensions.Options;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Application.AI;
using PulsePilot.Application.Common.Exceptions;
using PulsePilot.Application.Feedback;
using PulsePilot.Domain.Feedback;

namespace PulsePilot.Application.Tools;

internal sealed class SearchSimilarFeedbackTool(
    IFeedbackRepository feedbackRepository,
    IFeedbackEmbeddingRepository feedbackEmbeddingRepository,
    IOptions<SemanticSearchOptions> semanticSearchOptions) : ISearchSimilarFeedbackTool
{
    private readonly SemanticSearchOptions _semanticSearchOptions = semanticSearchOptions.Value;

    public async Task<SearchSimilarFeedbackToolResult> ExecuteAsync(
        Guid workspaceId,
        SearchSimilarFeedbackToolInput input,
        CancellationToken cancellationToken = default)
    {
        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("Workspace id is required.", nameof(workspaceId));
        }

        ArgumentNullException.ThrowIfNull(input);

        if (input.FeedbackId == Guid.Empty)
        {
            throw new ArgumentException("Feedback id is required.", nameof(input));
        }

        var feedback = await feedbackRepository.GetByIdAsync(
            workspaceId,
            input.FeedbackId,
            cancellationToken)
            ?? throw new NotFoundException("Feedback", input.FeedbackId);
        var embedding = await feedbackEmbeddingRepository.GetByFeedbackIdAsync(
            workspaceId,
            input.FeedbackId,
            cancellationToken);
        var currentSource = FeedbackEmbeddingSource.CreateText(
            feedback.Title,
            feedback.Content);
        var currentSourceHash = FeedbackEmbeddingSource.ComputeHash(currentSource);

        if (feedback.ProcessingStatus != ProcessingStatus.Completed
            || embedding is null
            || !string.Equals(
                embedding.SourceHash,
                currentSourceHash,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException(
                "Similar feedback is available after the current feedback embedding is completed.");
        }

        var limit = input.Limit ?? _semanticSearchOptions.DefaultLimit;

        if (limit is < 1 || limit > _semanticSearchOptions.MaxLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(input),
                $"Similar feedback limit must be between 1 and {_semanticSearchOptions.MaxLimit}.");
        }

        var matches = await feedbackEmbeddingRepository.FindSimilarAsync(
            workspaceId,
            input.FeedbackId,
            _semanticSearchOptions.SimilarityThreshold,
            limit,
            cancellationToken);

        return new SearchSimilarFeedbackToolResult(
            input.FeedbackId,
            _semanticSearchOptions.SimilarityThreshold,
            matches.Select(match => new SearchSimilarFeedbackToolItem(
                match.FeedbackId,
                match.FeedbackClusterId,
                match.Title,
                match.Content,
                match.Source,
                match.Similarity,
                match.CreatedAt)).ToList());
    }
}
