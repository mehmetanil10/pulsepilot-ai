using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace PulsePilot.Api.ErrorHandling;

public static class ApiProblemDetailsFactory
{
    public const string CodeExtensionName = "code";
    public const string TraceIdExtensionName = "traceId";

    private const string ProblemTypeBase = "https://pulsepilot.ai/problems/";

    public static ProblemDetails Create(
        HttpContext httpContext,
        int status,
        string title,
        string detail,
        string code)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var problemDetails = new ProblemDetails
        {
            Type = CreateProblemType(code),
            Status = status,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path,
        };

        AddSafeExtensions(httpContext, problemDetails, code);
        AddResponseHeaders(httpContext);

        return problemDetails;
    }

    public static HttpValidationProblemDetails CreateValidation(
        HttpContext httpContext,
        IReadOnlyDictionary<string, string[]> errors)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(errors);

        var problemDetails = new HttpValidationProblemDetails(errors)
        {
            Type = CreateProblemType("validation_error"),
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation failed",
            Detail = "One or more request values are invalid.",
            Instance = httpContext.Request.Path,
        };

        AddSafeExtensions(httpContext, problemDetails, "validation_error");
        AddResponseHeaders(httpContext);

        return problemDetails;
    }

    public static HttpValidationProblemDetails CreateModelBindingValidation(
        HttpContext httpContext,
        IEnumerable<string> fieldNames)
    {
        ArgumentNullException.ThrowIfNull(fieldNames);

        var errors = fieldNames
            .Select(NormalizeFieldName)
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(
                fieldName => fieldName,
                _ => new[] { "The supplied value is invalid." },
                StringComparer.Ordinal);

        if (errors.Count == 0)
        {
            errors.Add("request", ["The request body or parameters are invalid."]);
        }

        return CreateValidation(httpContext, errors);
    }

    public static void Customize(ProblemDetailsContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var problemDetails = context.ProblemDetails;
        var status = problemDetails.Status
            ?? context.HttpContext.Response.StatusCode;
        var hasConfiguredCode = problemDetails.Extensions.TryGetValue(
            CodeExtensionName,
            out var configuredCode)
            && configuredCode is string value
            && !string.IsNullOrWhiteSpace(value);
        var code = hasConfiguredCode
            ? (string)configuredCode!
            : GetDefaultCode(status);

        problemDetails.Status = status;
        if (!hasConfiguredCode || string.IsNullOrWhiteSpace(problemDetails.Type))
        {
            problemDetails.Type = CreateProblemType(code);
        }

        problemDetails.Instance ??= context.HttpContext.Request.Path;
        AddSafeExtensions(context.HttpContext, problemDetails, code);
        AddResponseHeaders(context.HttpContext);
    }

    private static void AddSafeExtensions(
        HttpContext httpContext,
        ProblemDetails problemDetails,
        string code)
    {
        problemDetails.Extensions.TryAdd(CodeExtensionName, code);
        problemDetails.Extensions.TryAdd(
            TraceIdExtensionName,
            httpContext.TraceIdentifier);
    }

    private static void AddResponseHeaders(HttpContext httpContext)
    {
        httpContext.Response.Headers.CacheControl = "no-store";
        httpContext.Response.Headers.XContentTypeOptions = "nosniff";
    }

    private static string NormalizeFieldName(string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            return "request";
        }

        const int maximumFieldNameLength = 100;
        var normalized = fieldName.Trim();

        return normalized.Length <= maximumFieldNameLength
            ? normalized
            : normalized[..maximumFieldNameLength];
    }

    private static string CreateProblemType(string code)
    {
        return $"{ProblemTypeBase}{code}";
    }

    private static string GetDefaultCode(int status)
    {
        return status switch
        {
            StatusCodes.Status400BadRequest => "bad_request",
            StatusCodes.Status401Unauthorized => "authentication_required",
            StatusCodes.Status403Forbidden => "access_denied",
            StatusCodes.Status404NotFound => "not_found",
            StatusCodes.Status409Conflict => "conflict",
            StatusCodes.Status422UnprocessableEntity => "business_rule_violation",
            StatusCodes.Status502BadGateway => "upstream_error",
            StatusCodes.Status503ServiceUnavailable => "service_unavailable",
            _ when status >= StatusCodes.Status500InternalServerError => "internal_error",
            _ => "request_failed",
        };
    }
}
