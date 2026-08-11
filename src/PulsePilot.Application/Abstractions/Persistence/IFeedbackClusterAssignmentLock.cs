namespace PulsePilot.Application.Abstractions.Persistence;

public interface IFeedbackClusterAssignmentLock
{
    Task<T> ExecuteAsync<T>(
        Guid workspaceId,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);
}
