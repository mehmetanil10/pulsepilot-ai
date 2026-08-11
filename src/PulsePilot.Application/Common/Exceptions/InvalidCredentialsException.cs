namespace PulsePilot.Application.Common.Exceptions;

public sealed class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException()
        : base("The email address or password is incorrect.")
    {
    }
}
