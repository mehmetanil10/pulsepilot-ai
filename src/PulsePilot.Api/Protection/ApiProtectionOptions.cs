namespace PulsePilot.Api.Protection;

public sealed class ApiProtectionOptions
{
    public const string SectionName = "ApiProtection";

    public bool RateLimitingEnabled { get; init; } = true;

    public int GeneralPermitLimit { get; init; } = 240;

    public int AuthenticationPermitLimit { get; init; } = 10;

    public int AiPermitLimit { get; init; } = 20;

    public int WindowSeconds { get; init; } = 60;

    public int QueueLimit { get; init; }

    public long MaxRequestBodyBytes { get; init; } = 65_536;
}
