using Npgsql;

namespace PulsePilot.Infrastructure.Persistence;

public static class DatabaseConnectionString
{
    private const int DefaultPostgreSqlPort = 5432;

    public static string Normalize(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        if (!connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return connectionString;
        }

        if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var uri)
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new InvalidOperationException("The PostgreSQL database URL is invalid.");
        }

        var credentials = uri.UserInfo.Split(':', 2);
        var database = Uri.UnescapeDataString(uri.AbsolutePath.Trim('/'));

        if (credentials.Length != 2
            || string.IsNullOrWhiteSpace(credentials[0])
            || string.IsNullOrWhiteSpace(database))
        {
            throw new InvalidOperationException(
                "The PostgreSQL database URL must include username, password, and database.");
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : DefaultPostgreSqlPort,
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = Uri.UnescapeDataString(credentials[1]),
            Database = database,
            IncludeErrorDetail = false,
        };

        return builder.ConnectionString;
    }
}
