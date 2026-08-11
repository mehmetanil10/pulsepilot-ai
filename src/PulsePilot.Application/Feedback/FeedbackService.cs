using Microsoft.Extensions.Options;
using PulsePilot.Application.Abstractions.Authentication;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Application.AI;
using PulsePilot.Application.Common.Exceptions;
using PulsePilot.Domain.Feedback;

using FeedbackEntity = PulsePilot.Domain.Feedback.Feedback;

namespace PulsePilot.Application.Feedback;

internal sealed class FeedbackService(
    IFeedbackRepository feedbackRepository,
    IFeedbackAnalysisRepository feedbackAnalysisRepository,
    IFeedbackEmbeddingRepository feedbackEmbeddingRepository,
    IUnitOfWork unitOfWork,
    ICurrentUserContext currentUser,
    TimeProvider timeProvider,
    IOptions<SemanticSearchOptions> semanticSearchOptions) : IFeedbackService
{
    private readonly SemanticSearchOptions _semanticSearchOptions = semanticSearchOptions.Value;

    public async Task<FeedbackResponse> CreateAsync(
        CreateFeedbackCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var feedback = FeedbackEntity.Create(
            currentUser.WorkspaceId,
            currentUser.UserId,
            command.Title,
            command.Content,
            command.Source,
            command.CustomerName,
            command.CustomerEmail,
            timeProvider.GetUtcNow());

        await feedbackRepository.AddAsync(feedback, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return FeedbackResponse.FromEntity(feedback);
    }

    public async Task<FeedbackListResponse> ListAsync(
        ListFeedbackQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var workspaceId = currentUser.WorkspaceId;
        var skip = (int)((long)(query.Page - 1) * query.PageSize);
        var totalCount = await feedbackRepository.CountAsync(
            workspaceId,
            query.Source,
            query.ProcessingStatus,
            cancellationToken);
        var feedback = await feedbackRepository.ListAsync(
            workspaceId,
            skip,
            query.PageSize,
            query.Source,
            query.ProcessingStatus,
            cancellationToken);

        return new FeedbackListResponse(
            feedback.Select(FeedbackResponse.FromEntity).ToList(),
            query.Page,
            query.PageSize,
            totalCount);
    }

    public async Task<FeedbackResponse> GetByIdAsync(
        Guid feedbackId,
        CancellationToken cancellationToken = default)
    {
        var feedback = await GetRequiredFeedbackAsync(feedbackId, cancellationToken);

        return FeedbackResponse.FromEntity(feedback);
    }

    public async Task<FeedbackAnalysisResponse> GetAnalysisAsync(
        Guid feedbackId,
        CancellationToken cancellationToken = default)
    {
        var feedback = await GetRequiredFeedbackAsync(feedbackId, cancellationToken);
        var analysis = await feedbackAnalysisRepository.GetByFeedbackIdAsync(
            currentUser.WorkspaceId,
            feedbackId,
            cancellationToken);

        return FeedbackAnalysisResponse.FromEntities(feedback, analysis);
    }

    public async Task<SimilarFeedbackResponse> GetSimilarAsync(
        Guid feedbackId,
        SimilarFeedbackQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var feedback = await GetRequiredFeedbackAsync(feedbackId, cancellationToken);
        var embedding = await feedbackEmbeddingRepository.GetByFeedbackIdAsync(
            currentUser.WorkspaceId,
            feedbackId,
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

        var limit = query.Limit ?? _semanticSearchOptions.DefaultLimit;

        if (limit is < 1 || limit > _semanticSearchOptions.MaxLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                $"Similar feedback limit must be between 1 and {_semanticSearchOptions.MaxLimit}.");
        }

        var matches = await feedbackEmbeddingRepository.FindSimilarAsync(
            currentUser.WorkspaceId,
            feedbackId,
            _semanticSearchOptions.SimilarityThreshold,
            limit,
            cancellationToken);

        return new SimilarFeedbackResponse(
            feedbackId,
            _semanticSearchOptions.SimilarityThreshold,
            matches.Select(match => new SimilarFeedbackItemResponse(
                match.FeedbackId,
                match.FeedbackClusterId,
                match.Title,
                match.Content,
                match.Source,
                match.Similarity,
                match.CreatedAt)).ToList());
    }

    public async Task<FeedbackResponse> RetryAnalysisAsync(
        Guid feedbackId,
        CancellationToken cancellationToken = default)
    {
        var feedback = await GetRequiredFeedbackAsync(feedbackId, cancellationToken);

        if (feedback.ProcessingStatus != PulsePilot.Domain.Feedback.ProcessingStatus.Failed)
        {
            throw new ConflictException(
                "Only failed feedback analysis can be queued for retry.");
        }

        feedback.RetryProcessing(timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return FeedbackResponse.FromEntity(feedback);
    }

    public async Task<FeedbackResponse> UpdateAsync(
        Guid feedbackId,
        UpdateFeedbackCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var feedback = await GetRequiredFeedbackAsync(feedbackId, cancellationToken);
        feedback.UpdateDetails(
            command.Title,
            command.Content,
            command.Source,
            command.CustomerName,
            command.CustomerEmail,
            timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return FeedbackResponse.FromEntity(feedback);
    }

    public async Task DeleteAsync(
        Guid feedbackId,
        CancellationToken cancellationToken = default)
    {
        var feedback = await GetRequiredFeedbackAsync(feedbackId, cancellationToken);
        feedback.MarkDeleted(timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<FeedbackEntity> GetRequiredFeedbackAsync(
        Guid feedbackId,
        CancellationToken cancellationToken)
    {
        return await feedbackRepository.GetByIdAsync(
            currentUser.WorkspaceId,
            feedbackId,
            cancellationToken)
            ?? throw new NotFoundException("Feedback", feedbackId);
    }
}
