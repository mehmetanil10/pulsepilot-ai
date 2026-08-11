using PulsePilot.Infrastructure.Persistence.Seeding;

namespace PulsePilot.Api.Database;

public static class DemoDataSeedExtensions
{
    public static async Task SeedDemoDataAsync(
        this WebApplication application,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(application);

        await using var scope = application.Services.CreateAsyncScope();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("DemoDataSeed");
        var seeder = scope.ServiceProvider.GetRequiredService<IDemoDataSeeder>();

        logger.LogInformation("Seeding demo workspace data");
        var result = await seeder.SeedAsync(cancellationToken);
        logger.LogInformation(
            "Demo data seed completed for workspace {WorkspaceId}: {AddedFeedbackCount} added, {TotalFeedbackCount} total feedback",
            result.WorkspaceId,
            result.AddedFeedbackCount,
            result.TotalFeedbackCount);
    }
}
