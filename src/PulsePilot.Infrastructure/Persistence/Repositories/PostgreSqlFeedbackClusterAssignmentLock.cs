using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using PulsePilot.Application.Abstractions.Persistence;

namespace PulsePilot.Infrastructure.Persistence.Repositories;

internal sealed class PostgreSqlFeedbackClusterAssignmentLock(AppDbContext dbContext)
    : IFeedbackClusterAssignmentLock
{
    public async Task<T> ExecuteAsync<T>(
        Guid workspaceId,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("Workspace id is required.", nameof(workspaceId));
        }

        var connection = dbContext.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != ConnectionState.Open;
        var (firstKey, secondKey) = CreateLockKeys(workspaceId);

        if (shouldCloseConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await SetLockAsync(
                connection,
                "SELECT pg_advisory_lock(@first_key, @second_key);",
                firstKey,
                secondKey,
                cancellationToken);

            try
            {
                return await operation(cancellationToken);
            }
            finally
            {
                await SetLockAsync(
                    connection,
                    "SELECT pg_advisory_unlock(@first_key, @second_key);",
                    firstKey,
                    secondKey,
                    CancellationToken.None);
            }
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task SetLockAsync(
        DbConnection connection,
        string commandText,
        int firstKey,
        int secondKey,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        AddParameter(command, "first_key", firstKey);
        AddParameter(command, "second_key", secondKey);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static (int First, int Second) CreateLockKeys(Guid workspaceId)
    {
        Span<byte> bytes = stackalloc byte[16];
        workspaceId.TryWriteBytes(bytes);

        return (
            BitConverter.ToInt32(bytes[..4]),
            BitConverter.ToInt32(bytes.Slice(4, 4)));
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
