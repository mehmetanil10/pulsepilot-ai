namespace PulsePilot.Application.Common.Exceptions;

public sealed class NotFoundException : Exception
{
    public NotFoundException(string message)
        : base(message)
    {
    }

    public NotFoundException(string resourceName, object key)
        : this($"{resourceName} with key '{key}' was not found.")
    {
    }
}
