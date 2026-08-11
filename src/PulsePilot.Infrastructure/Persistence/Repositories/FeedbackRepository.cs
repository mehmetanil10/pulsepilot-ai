using Microsoft.EntityFrameworkCore;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Domain.Feedback;

using FeedbackEntity = PulsePilot.Domain.Feedback.Feedback;

namespace PulsePilot.Infrastructure.Persistence.Repositories;

internal sealed class FeedbackRepository : IFeedbackRepository
{
    private readonly AppDbContext _dbContext;

    public FeedbackRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<FeedbackEntity?> GetByIdAsync(
        Guid workspaceId,
        Guid feedbackId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Feedback.SingleOrDefaultAsync(
            feedback => feedback.Id == feedbackId && feedback.WorkspaceId == workspaceId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<FeedbackEntity>> ListAsync(
        Guid workspaceId,
        int skip,
        int take,
        FeedbackSource? source = null,
        ProcessingStatus? processingStatus = null,
        CancellationToken cancellationToken = default)
    {
        if (skip < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(skip), "Skip cannot be negative.");
        }

        if (take <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(take), "Take must be greater than zero.");
        }

        var query = BuildFilteredQuery(workspaceId, source, processingStatus);

        return await query
            .OrderByDescending(feedback => feedback.CreatedAt)
            .ThenByDescending(feedback => feedback.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(
        Guid workspaceId,
        FeedbackSource? source = null,
        ProcessingStatus? processingStatus = null,
        CancellationToken cancellationToken = default)
    {
        return BuildFilteredQuery(workspaceId, source, processingStatus)
            .CountAsync(cancellationToken);
    }

    public async Task AddAsync(
        FeedbackEntity feedback,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.Feedback.AddAsync(feedback, cancellationToken);
    }

    private IQueryable<FeedbackEntity> BuildFilteredQuery(
        Guid workspaceId,
        FeedbackSource? source,
        ProcessingStatus? processingStatus)
    {
        var query = _dbContext.Feedback
            .AsNoTracking()
            .Where(feedback => feedback.WorkspaceId == workspaceId);

        if (source.HasValue)
        {
            query = query.Where(feedback => feedback.Source == source.Value);
        }

        if (processingStatus.HasValue)
        {
            query = query.Where(feedback => feedback.ProcessingStatus == processingStatus.Value);
        }

        return query;
    }
}
