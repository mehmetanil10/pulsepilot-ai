namespace PulsePilot.Application.Agents;

public sealed class AgentOrchestrationOptions
{
    public const string SectionName = "AgentOrchestration";
    public const int MaximumAllowedTurns = 12;
    public const int MaximumAllowedToolCallsPerTurn = 8;
    public const int MaximumAllowedTotalToolCalls = 24;
    public const int MaximumAllowedTimeoutSeconds = 300;
    public const int MaximumAllowedUserMessageLength = 8_000;
    public const int MaximumAllowedFinalAnswerLength = 20_000;
    public const int MaximumAllowedToolArgumentsLength = 16_000;
    public const int MaximumAllowedToolOutputLength = 50_000;

    public int MaxTurns { get; set; } = 6;

    public int MaxToolCallsPerTurn { get; set; } = 4;

    public int MaxTotalToolCalls { get; set; } = 12;

    public int ExecutionTimeoutSeconds { get; set; } = 90;

    public int MaxUserMessageLength { get; set; } = 4_000;

    public int MaxFinalAnswerLength { get; set; } = 8_000;

    public int MaxToolArgumentsLength { get; set; } = 8_000;

    public int MaxToolOutputLength { get; set; } = 20_000;
}
