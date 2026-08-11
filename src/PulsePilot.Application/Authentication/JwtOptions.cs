namespace PulsePilot.Application.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int MinimumSecretSizeInBytes = 32;

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    public string Secret { get; init; } = string.Empty;

    public int ExpirationMinutes { get; init; } = 60;
}
