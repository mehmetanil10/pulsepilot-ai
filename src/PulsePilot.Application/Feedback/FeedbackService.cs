using PulsePilot.Application.Abstractions.Authentication;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Application.Common.Exceptions;
using PulsePilot.Application.Prioritization;
using PulsePilot.Application.Tools;
using PulsePilot.Domain.Feedback;

using FeedbackEntity = PulsePilot.Domain.Feedback.Feedback;

namespace PulsePilot.Application.Feedback;

internal sealed class FeedbackService(
    IFeedbackRepository feedbackRepository,
    IFeedbackListRepository feedbackListRepository,
    IFeedbackAnalysisRepository feedbackAnalysisRepository,
    IFeedbackClusterRepository feedbackClusterRepository,
    IFeedbackClusterAssignmentLock clusterAssignmentLock,
    IPriorityScoreCalculator priorityScoreCalculator,
    ISearchSimilarFeedbackTool searchSimilarFeedbackTool,
    IUnitOfWork unitOfWork,
    ICurrentUserContext currentUser,
    TimeProvider timeProvider) : IFeedbackService
{
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
        DateTimeOffset? dateFrom = query.DateFrom.HasValue
            ? new DateTimeOffset(
                query.DateFrom.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))
            : null;
        DateTimeOffset? dateTo = query.DateTo.HasValue
            ? new DateTimeOffset(
                query.DateTo.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))
            : null;
        var page = await feedbackListRepository.GetPageAsync(
            workspaceId,
            new FeedbackListFilter(
                query.Source,
                query.ProcessingStatus,
                query.Category,
                query.Component,
                query.Severity,
                query.Sentiment,
                dateFrom,
                dateTo,
                query.Search),
            skip,
            query.PageSize,
            cancellationToken);

        return new FeedbackListResponse(
            page.Items.Select(item => new FeedbackListItemResponse(
                item.Id,
                item.FeedbackClusterId,
                item.Title,
                item.Content,
                item.Source,
                item.ProcessingStatus,
                item.CreatedAt,
                item.UpdatedAt,
                item.Category,
                item.Component,
                item.Severity,
                item.Sentiment)).ToList(),
            query.Page,
            query.PageSize,
            page.TotalCount);
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

        var result = await searchSimilarFeedbackTool.ExecuteAsync(
            currentUser.WorkspaceId,
            new SearchSimilarFeedbackToolInput(feedbackId, query.Limit),
            cancellationToken);

        return new SimilarFeedbackResponse(
            result.FeedbackId,
            result.SimilarityThreshold,
            result.Items.Select(item => new SimilarFeedbackItemResponse(
                item.FeedbackId,
                item.FeedbackClusterId,
                item.Title,
                item.Content,
                item.Source,
                item.Similarity,
                item.CreatedAt)).ToList());
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

        return await clusterAssignmentLock.ExecuteAsync(
            currentUser.WorkspaceId,
            async token =>
            {
                var feedback = await GetRequiredFeedbackAsync(feedbackId, token);
                var previousClusterId = feedback.FeedbackClusterId;
                var updatedAt = timeProvider.GetUtcNow();
                feedback.UpdateDetails(
                    command.Title,
                    command.Content,
                    command.Source,
                    command.CustomerName,
                    command.CustomerEmail,
                    updatedAt);
                await RecalculateClusterAfterRemovalAsync(
                    previousClusterId,
                    feedback.Id,
                    updatedAt,
                    token);
                await unitOfWork.SaveChangesAsync(token);

                return FeedbackResponse.FromEntity(feedback);
            },
            cancellationToken);
    }

    public async Task DeleteAsync(
        Guid feedbackId,
        CancellationToken cancellationToken = default)
    {
        await clusterAssignmentLock.ExecuteAsync(
            currentUser.WorkspaceId,
            async token =>
            {
                var feedback = await GetRequiredFeedbackAsync(feedbackId, token);
                var previousClusterId = feedback.FeedbackClusterId;
                var deletedAt = timeProvider.GetUtcNow();
                feedback.MarkDeleted(deletedAt);
                await RecalculateClusterAfterRemovalAsync(
                    previousClusterId,
                    feedback.Id,
                    deletedAt,
                    token);
                await unitOfWork.SaveChangesAsync(token);

                return true;
            },
            cancellationToken);
    }

    private async Task RecalculateClusterAfterRemovalAsync(
        Guid? clusterId,
        Guid removedFeedbackId,
        DateTimeOffset calculatedAt,
        CancellationToken cancellationToken)
    {
        if (!clusterId.HasValue)
        {
            return;
        }

        var cluster = await feedbackClusterRepository.GetByIdAsync(
            currentUser.WorkspaceId,
            clusterId.Value,
            cancellationToken);

        if (cluster is null)
        {
            return;
        }

        var members = await feedbackClusterRepository.ListPriorityScoringMembersAsync(
            currentUser.WorkspaceId,
            clusterId.Value,
            [],
            [removedFeedbackId],
            cancellationToken);

        if (members.Count == 0)
        {
            cluster.UpdatePriority(0m, FeedbackPriority.P4, calculatedAt);
            return;
        }

        var priority = priorityScoreCalculator.Calculate(members, calculatedAt);
        cluster.UpdatePriority(priority.Score, priority.Priority, calculatedAt);
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
