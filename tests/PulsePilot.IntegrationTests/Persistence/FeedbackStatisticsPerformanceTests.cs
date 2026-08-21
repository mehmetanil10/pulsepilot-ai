using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Infrastructure.Persistence.Seeding;

namespace PulsePilot.IntegrationTests.Persistence;

public sealed class FeedbackStatisticsPerformanceTests(PostgreSqlFixture database)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task StatisticsSnapshot_UsesAtMostTwoDatabaseCommands()
    {
        var counter = new CommandCounterInterceptor();
        var configuration = new Dictionary<string, string?>
        {
            ["Seed:Email"] = "performance@pulsepilot.ai",
            ["Seed:Password"] = "performance-test-password",
            ["Seed:FeedbackCount"] = "100",
        };
        await using var provider = database.CreateServiceProvider(
            configuration,
            services => services.AddSingleton<IInterceptor>(counter));
        await using var scope = provider.CreateAsyncScope();
        var seedResult = await scope.ServiceProvider
            .GetRequiredService<IDemoDataSeeder>()
            .SeedAsync();
        counter.Reset();

        var snapshot = await scope.ServiceProvider
            .GetRequiredService<IFeedbackStatisticsRepository>()
            .GetAsync(
                seedResult.WorkspaceId,
                DateTimeOffset.UtcNow.AddYears(-1),
                DateTimeOffset.UtcNow.AddYears(1));

        Assert.Equal(100, snapshot.TotalFeedbackCount);
        Assert.True(snapshot.AnalyzedFeedbackCount > 0);
        Assert.Equal(2, counter.ReaderCommandCount);
    }

    private sealed class CommandCounterInterceptor : DbCommandInterceptor
    {
        private int _readerCommandCount;

        public int ReaderCommandCount => Volatile.Read(ref _readerCommandCount);

        public void Reset()
        {
            Interlocked.Exchange(ref _readerCommandCount, 0);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _readerCommandCount);
            return base.ReaderExecutingAsync(
                command,
                eventData,
                result,
                cancellationToken);
        }
    }
}
