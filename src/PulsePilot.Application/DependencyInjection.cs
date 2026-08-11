using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PulsePilot.Application.Authentication;

namespace PulsePilot.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining(typeof(DependencyInjection));
        services.AddScoped<IAuthService, AuthService>();
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
