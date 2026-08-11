using Microsoft.EntityFrameworkCore;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Application.FeedbackProcessing;
using PulsePilot.Domain.Feedback;

namespace PulsePilot.Infrastructure.Persistence.Repositories;

internal sealed class FeedbackProcessingQueue(AppDbContext dbContext)
    : IFeedbackProcessingQueue
{
    public async Task<FeedbackProcessingItem?> ClaimNextPendingAsync(
        DateTimeOffset claimedAt,
        CancellationToken cancellationToken = default)
    {
        var processingLeaseId = Guid.CreateVersion7();
        var connection = dbContext.Database.GetDbConnection();
        await dbContext.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                WITH candidate AS (
                    SELECT id
                    FROM feedback
                    WHERE processing_status = 'Pending'
                      AND deleted_at IS NULL
                    ORDER BY created_at, id
                    LIMIT 1
                    FOR UPDATE SKIP LOCKED
                )
                UPDATE feedback AS target
                SET processing_status = 'Processing',
                    processing_lease_id = @processing_lease_id,
                    processing_started_at = @claimed_at,
                    updated_at = @claimed_at
                FROM candidate
                WHERE target.id = candidate.id
                RETURNING
                    target.id,
                    target.workspace_id,
                    target.title,
                    target.content,
                    target.source
                """;
            AddParameter(command, "processing_lease_id", processingLeaseId);
            AddParameter(command, "claimed_at", claimedAt);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new FeedbackProcessingItem(
                reader.GetGuid(0),
                reader.GetGuid(1),
                processingLeaseId,
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetString(3),
                Enum.Parse<FeedbackSource>(reader.GetString(4)));
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    public Task<int> RecoverStaleAsync(
        DateTimeOffset staleBefore,
        DateTimeOffset recoveredAt,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        if (maxCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxCount),
                "Recovery batch size must be greater than zero.");
        }

        return dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             WITH stale_feedback AS (
                 SELECT id
                 FROM feedback
                 WHERE processing_status = 'Processing'
                   AND processing_started_at <= {staleBefore}
                   AND deleted_at IS NULL
                 ORDER BY processing_started_at, id
                 LIMIT {maxCount}
                 FOR UPDATE SKIP LOCKED
             )
             UPDATE feedback AS target
             SET processing_status = 'Pending',
                 processing_lease_id = NULL,
                 processing_started_at = NULL,
                 updated_at = {recoveredAt}
             FROM stale_feedback
             WHERE target.id = stale_feedback.id
             """,
            cancellationToken);
    }

    private static void AddParameter(
        System.Data.Common.DbCommand command,
        string name,
        object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
