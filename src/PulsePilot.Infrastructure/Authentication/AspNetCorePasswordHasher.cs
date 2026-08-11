using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PulsePilot.Application.Abstractions.Authentication;

namespace PulsePilot.Infrastructure.Authentication;

internal sealed class AspNetCorePasswordHasher : IPasswordHasher
{
    private static readonly object PasswordOwner = new();

    private readonly PasswordHasher<object> _passwordHasher;
    private readonly string _dummyPasswordHash;

    public AspNetCorePasswordHasher(IOptions<PasswordHasherOptions> options)
    {
        _passwordHasher = new PasswordHasher<object>(options);
        _dummyPasswordHash = _passwordHasher.HashPassword(
            PasswordOwner,
            Guid.CreateVersion7().ToString("N"));
    }

    public string HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        return _passwordHasher.HashPassword(PasswordOwner, password);
    }

    public PasswordVerificationStatus VerifyPassword(
        string? passwordHash,
        string providedPassword)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providedPassword);

        var verificationResult = _passwordHasher.VerifyHashedPassword(
            PasswordOwner,
            passwordHash ?? _dummyPasswordHash,
            providedPassword);

        if (passwordHash is null)
        {
            return PasswordVerificationStatus.Failed;
        }

        return verificationResult switch
        {
            PasswordVerificationResult.Success => PasswordVerificationStatus.Success,
            PasswordVerificationResult.SuccessRehashNeeded =>
                PasswordVerificationStatus.SuccessRehashNeeded,
            _ => PasswordVerificationStatus.Failed,
        };
    }
}
