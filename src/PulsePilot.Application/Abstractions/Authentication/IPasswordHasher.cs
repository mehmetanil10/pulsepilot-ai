namespace PulsePilot.Application.Abstractions.Authentication;

public interface IPasswordHasher
{
    string HashPassword(string password);

    PasswordVerificationStatus VerifyPassword(string? passwordHash, string providedPassword);
}
