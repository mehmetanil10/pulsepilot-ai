namespace PulsePilot.Application.Abstractions.Persistence;

public interface IPendingActionExecutionLock
{
    Task<T> ExecuteAsync<T>(
        Guid pendingActionId,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);
}
