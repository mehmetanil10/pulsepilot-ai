namespace PulsePilot.Api.Protection;

public sealed class ApiSecurityHeadersMiddleware(RequestDelegate next)
{
    private const string ContentSecurityPolicy =
        "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Path.StartsWithSegments("/api")
            || context.Request.Path.StartsWithSegments("/health"))
        {
            context.Response.OnStarting(static state =>
            {
                var response = (HttpResponse)state;
                response.Headers.CacheControl = "no-store";
                response.Headers.ContentSecurityPolicy = ContentSecurityPolicy;
                response.Headers["Referrer-Policy"] = "no-referrer";
                response.Headers.XContentTypeOptions = "nosniff";
                response.Headers.XFrameOptions = "DENY";
                response.Headers.Append(
                    "Permissions-Policy",
                    "camera=(), geolocation=(), microphone=()");

                return Task.CompletedTask;
            }, context.Response);
        }

        return next(context);
    }
}

public static class ApiSecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseApiSecurityHeaders(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ApiSecurityHeadersMiddleware>();
    }
}
