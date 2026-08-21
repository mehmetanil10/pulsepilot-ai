using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PulsePilot.Application.Common.Exceptions;
using PulsePilot.Domain.Common;

namespace PulsePilot.Api.ErrorHandling;

public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        var problemDetails = CreateProblemDetails(httpContext, exception);

        if (problemDetails.Status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(
                "An unhandled {ErrorType} occurred while processing {RequestMethod} {RequestPath}; trace {TraceId}",
                exception.GetType().Name,
                httpContext.Request.Method,
                httpContext.Request.Path,
                httpContext.TraceIdentifier);
        }
        else
        {
            logger.LogWarning(
                "Request {RequestMethod} {RequestPath} failed with status code {StatusCode}: {ErrorType}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                problemDetails.Status,
                exception.GetType().Name);
        }

        httpContext.Response.StatusCode = problemDetails.Status
            ?? StatusCodes.Status500InternalServerError;

        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception,
        });

        return true;
    }

    private static ProblemDetails CreateProblemDetails(
        HttpContext httpContext,
        Exception exception)
    {
        return exception switch
        {
            ValidationException validationException => CreateValidationProblemDetails(
                httpContext,
                validationException),
            InvalidCredentialsException => ApiProblemDetailsFactory.Create(
                httpContext,
                StatusCodes.Status401Unauthorized,
                "Authentication failed",
                "The email address or password is incorrect.",
                "invalid_credentials"),
            NotFoundException => ApiProblemDetailsFactory.Create(
                httpContext,
                StatusCodes.Status404NotFound,
                "Resource not found",
                "The requested resource could not be found.",
                "not_found"),
            ForbiddenException => ApiProblemDetailsFactory.Create(
                httpContext,
                StatusCodes.Status403Forbidden,
                "Access denied",
                "The authenticated user is not allowed to perform this operation.",
                "access_denied"),
            ConcurrencyConflictException => ApiProblemDetailsFactory.Create(
                httpContext,
                StatusCodes.Status409Conflict,
                "Concurrent update conflict",
                "The resource changed while the request was being processed. Retry the operation.",
                "concurrency_conflict"),
            ConflictException => ApiProblemDetailsFactory.Create(
                httpContext,
                StatusCodes.Status409Conflict,
                "Conflict",
                "The request conflicts with the current resource state.",
                "conflict"),
            LlmProviderException llmException => ApiProblemDetailsFactory.Create(
                httpContext,
                llmException.IsTransient
                    || llmException.FailureKind == LlmProviderFailureKind.NotConfigured
                    ? StatusCodes.Status503ServiceUnavailable
                    : StatusCodes.Status502BadGateway,
                "AI provider unavailable",
                "The AI provider could not produce the requested output.",
                "ai_provider_unavailable"),
            DomainException => ApiProblemDetailsFactory.Create(
                httpContext,
                StatusCodes.Status422UnprocessableEntity,
                "Business rule violation",
                "The request cannot be completed because a business rule was violated.",
                "business_rule_violation"),
            BadHttpRequestException => ApiProblemDetailsFactory.Create(
                httpContext,
                StatusCodes.Status400BadRequest,
                "Bad request",
                "The request body or parameters could not be read.",
                "bad_request"),
            _ => ApiProblemDetailsFactory.Create(
                httpContext,
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred",
                "The server could not complete the request.",
                "internal_error"),
        };
    }

    private static HttpValidationProblemDetails CreateValidationProblemDetails(
        HttpContext httpContext,
        ValidationException exception)
    {
        var errors = exception.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).Distinct().ToArray());

        return ApiProblemDetailsFactory.CreateValidation(httpContext, errors);
    }
}
