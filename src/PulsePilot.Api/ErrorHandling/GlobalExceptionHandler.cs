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
                exception,
                "An unhandled exception occurred while processing {RequestMethod} {RequestPath}",
                httpContext.Request.Method,
                httpContext.Request.Path);
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

    private static ProblemDetails CreateProblemDetails(HttpContext httpContext, Exception exception)
    {
        ProblemDetails problemDetails = exception switch
        {
            ValidationException validationException => CreateValidationProblemDetails(validationException),
            InvalidCredentialsException => CreateKnownProblemDetails(
                StatusCodes.Status401Unauthorized,
                "Authentication failed",
                exception.Message),
            NotFoundException => CreateKnownProblemDetails(
                StatusCodes.Status404NotFound,
                "Resource not found",
                exception.Message),
            ForbiddenException => CreateKnownProblemDetails(
                StatusCodes.Status403Forbidden,
                "Access denied",
                exception.Message),
            ConcurrencyConflictException => CreateKnownProblemDetails(
                StatusCodes.Status409Conflict,
                "Conflict",
                exception.Message),
            ConflictException => CreateKnownProblemDetails(
                StatusCodes.Status409Conflict,
                "Conflict",
                exception.Message),
            DomainException => CreateKnownProblemDetails(
                StatusCodes.Status422UnprocessableEntity,
                "Business rule violation",
                exception.Message),
            BadHttpRequestException => CreateKnownProblemDetails(
                StatusCodes.Status400BadRequest,
                "Bad request",
                exception.Message),
            _ => CreateKnownProblemDetails(
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred",
                "The server could not complete the request."),
        };

        problemDetails.Instance = httpContext.Request.Path;
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        return problemDetails;
    }

    private static ProblemDetails CreateKnownProblemDetails(int status, string title, string detail)
    {
        return new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
        };
    }

    private static HttpValidationProblemDetails CreateValidationProblemDetails(
        ValidationException exception)
    {
        var errors = exception.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).Distinct().ToArray());

        return new HttpValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation failed",
            Detail = "One or more validation errors occurred.",
        };
    }
}
