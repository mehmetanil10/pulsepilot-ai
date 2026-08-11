namespace PulsePilot.Infrastructure.Persistence.Seeding;

public sealed record DemoSeedResult(
    Guid UserId,
    Guid WorkspaceId,
    int AddedFeedbackCount,
    int TotalFeedbackCount);
