namespace PulsePilot.Application.Authentication;

public interface IAuthService
{
    Task<AuthenticationResponse> RegisterAsync(
        RegisterCommand command,
        CancellationToken cancellationToken = default);

    Task<AuthenticationResponse> LoginAsync(
        LoginCommand command,
        CancellationToken cancellationToken = default);
}
