using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PulsePilot.Api.Validation;

namespace PulsePilot.IntegrationTests.Api;

public sealed class ValidationInfrastructureTests
{
    [Fact]
    public async Task ActionFilter_InvalidRequest_ThrowsValidationExceptionBeforeActionRuns()
    {
        var validator = new InlineValidator<TestRequest>();
        validator.RuleFor(request => request.Name).NotEmpty();

        await using var serviceProvider = new ServiceCollection()
            .AddSingleton<IValidator<TestRequest>>(validator)
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = serviceProvider,
        };
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor(),
            new ModelStateDictionary());
        var executingContext = new ActionExecutingContext(
            actionContext,
            [],
            new Dictionary<string, object?>
            {
                ["request"] = new TestRequest(string.Empty),
            },
            new object());
        var actionRan = false;

        Task<ActionExecutedContext> Next()
        {
            actionRan = true;
            return Task.FromResult(new ActionExecutedContext(actionContext, [], new object()));
        }

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            new FluentValidationActionFilter().OnActionExecutionAsync(executingContext, Next));

        Assert.False(actionRan);
        Assert.Contains(exception.Errors, failure => failure.PropertyName == nameof(TestRequest.Name));
    }

    private sealed record TestRequest(string Name);
}
