using Npgsql;
using PulsePilot.Infrastructure.Persistence;

namespace PulsePilot.IntegrationTests.Persistence;

public sealed class DatabaseConnectionStringTests
{
    [Fact]
    public void Normalize_PreservesNpgsqlKeyValueConnectionString()
    {
        const string connectionString =
            "Host=database;Port=5432;Database=pulsepilot;Username=app;Password=secret";

        var normalized = DatabaseConnectionString.Normalize(connectionString);

        Assert.Equal(connectionString, normalized);
    }

    [Theory]
    [InlineData("postgres")]
    [InlineData("postgresql")]
    public void Normalize_ConvertsPlatformDatabaseUrl(string scheme)
    {
        var normalized = DatabaseConnectionString.Normalize(
            $"{scheme}://pulse%40pilot:p%3Ass%2Fword@db.internal:5544/pulse%2Dpilot");
        var builder = new NpgsqlConnectionStringBuilder(normalized);

        Assert.Equal("db.internal", builder.Host);
        Assert.Equal(5544, builder.Port);
        Assert.Equal("pulse@pilot", builder.Username);
        Assert.Equal("p:ss/word", builder.Password);
        Assert.Equal("pulse-pilot", builder.Database);
        Assert.False(builder.IncludeErrorDetail);
    }

    [Fact]
    public void Normalize_UsesDefaultPostgreSqlPort()
    {
        var normalized = DatabaseConnectionString.Normalize(
            "postgresql://app:secret@db.internal/pulsepilot");
        var builder = new NpgsqlConnectionStringBuilder(normalized);

        Assert.Equal(5432, builder.Port);
    }

    [Theory]
    [InlineData("postgresql://db.internal/pulsepilot")]
    [InlineData("postgresql://app:secret@db.internal/")]
    public void Normalize_RejectsIncompleteDatabaseUrl(string connectionString)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => DatabaseConnectionString.Normalize(connectionString));

        Assert.Contains("database URL", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
