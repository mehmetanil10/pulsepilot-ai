using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PulsePilot.Application.Abstractions.Authentication;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Infrastructure.Authentication;
using PulsePilot.Infrastructure.Persistence;
using PulsePilot.Infrastructure.Persistence.Repositories;

namespace PulsePilot.Infrastructure;

public static class DependencyInjection
{
    private const string DatabaseConnectionStringName = "Database";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions();

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            var runtimeConfiguration = serviceProvider.GetRequiredService<IConfiguration>();
            var connectionString = runtimeConfiguration.GetConnectionString(
                DatabaseConnectionStringName);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"Connection string '{DatabaseConnectionStringName}' is required.");
            }

            options.UseNpgsql(
                connectionString,
                npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorCodesToAdd: null);
                });
        });

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();
        services.AddScoped<IWorkspaceMemberRepository, WorkspaceMemberRepository>();
        services.AddScoped<IFeedbackRepository, FeedbackRepository>();
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddSingleton<IPasswordHasher, AspNetCorePasswordHasher>();
        services.AddSingleton<IAccessTokenGenerator, JwtAccessTokenGenerator>();

        return services;
    }
}
