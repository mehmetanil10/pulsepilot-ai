namespace PulsePilot.Application.Common.Exceptions;

public enum LlmProviderFailureKind
{
    NotConfigured = 1,
    Refused = 2,
    Incomplete = 3,
    InvalidResponse = 4,
    ProviderFailure = 5,
    ProviderUnavailable = 6,
}

public sealed class LlmProviderException : Exception
{
    public LlmProviderException(
        LlmProviderFailureKind failureKind,
        string message,
        bool isTransient = false,
        Exception? innerException = null)
        : base(message, innerException)
    {
        FailureKind = failureKind;
        IsTransient = isTransient;
    }

    public LlmProviderFailureKind FailureKind { get; }

    public bool IsTransient { get; }
}
