using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using PulsePilot.Application.Abstractions.Persistence;

namespace PulsePilot.Infrastructure.Persistence.Repositories;

internal sealed class PostgreSqlPendingActionExecutionLock(AppDbContext dbContext)
    : IPendingActionExecutionLock
{
    private const int LockNamespace = 1_347_437_123;

    public async Task<T> ExecuteAsync<T>(
        Guid pendingActionId,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (pendingActionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Pending action id is required.",
                nameof(pendingActionId));
        }

        var connection = dbContext.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != ConnectionState.Open;
        var lockKey = CreateLockKey(pendingActionId);

        if (shouldCloseConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await SetLockAsync(
                connection,
                "SELECT pg_advisory_lock(@lock_namespace, @lock_key);",
                lockKey,
                cancellationToken);

            try
            {
                return await operation(cancellationToken);
            }
            finally
            {
                await SetLockAsync(
                    connection,
                    "SELECT pg_advisory_unlock(@lock_namespace, @lock_key);",
                    lockKey,
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
        int lockKey,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        AddParameter(command, "lock_namespace", LockNamespace);
        AddParameter(command, "lock_key", lockKey);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static int CreateLockKey(Guid pendingActionId)
    {
        Span<byte> bytes = stackalloc byte[16];
        pendingActionId.TryWriteBytes(bytes);

        return BitConverter.ToInt32(bytes[..4])
            ^ BitConverter.ToInt32(bytes.Slice(4, 4))
            ^ BitConverter.ToInt32(bytes.Slice(8, 4))
            ^ BitConverter.ToInt32(bytes.Slice(12, 4));
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
