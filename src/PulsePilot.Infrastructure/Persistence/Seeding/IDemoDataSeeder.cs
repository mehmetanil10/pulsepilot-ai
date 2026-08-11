namespace PulsePilot.Infrastructure.Persistence.Seeding;

public interface IDemoDataSeeder
{
    Task<DemoSeedResult> SeedAsync(CancellationToken cancellationToken = default);
}
