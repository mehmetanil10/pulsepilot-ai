using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PulsePilot.Application.Authentication;
using PulsePilot.Application.Feedback;
using PulsePilot.Application.FeedbackProcessing;

namespace PulsePilot.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining(typeof(DependencyInjection));
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IFeedbackService, FeedbackService>();
        services.AddScoped<IFeedbackAnalysisProcessor, FeedbackAnalysisProcessor>();
        services.AddOptions<FeedbackProcessingOptions>();
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
