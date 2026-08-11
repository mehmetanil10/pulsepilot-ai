using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PulsePilot.Api.ErrorHandling;
using PulsePilot.Application.Common.Exceptions;

namespace PulsePilot.IntegrationTests.Api;

public sealed class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_NotFoundException_WritesNotFoundProblemDetails()
    {
        var problemDetailsService = new CapturingProblemDetailsService();
        var handler = new GlobalExceptionHandler(
            problemDetailsService,
            NullLogger<GlobalExceptionHandler>.Instance);
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "trace-not-found",
        };
        httpContext.Request.Path = "/workspaces/missing";

        var handled = await handler.TryHandleAsync(
            httpContext,
            new NotFoundException("Workspace", Guid.Empty),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status404NotFound, httpContext.Response.StatusCode);
        Assert.Equal("Resource not found", problemDetailsService.Written!.ProblemDetails.Title);
        Assert.Equal(
            "trace-not-found",
            problemDetailsService.Written.ProblemDetails.Extensions["traceId"]);
    }

    [Fact]
    public async Task TryHandleAsync_UnexpectedException_DoesNotExposeExceptionMessage()
    {
        var problemDetailsService = new CapturingProblemDetailsService();
        var handler = new GlobalExceptionHandler(
            problemDetailsService,
            NullLogger<GlobalExceptionHandler>.Instance);
        var httpContext = new DefaultHttpContext();

        var handled = await handler.TryHandleAsync(
            httpContext,
            new InvalidOperationException("sensitive database detail"),
            CancellationToken.None);

        var problemDetails = problemDetailsService.Written!.ProblemDetails;

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);
        Assert.DoesNotContain("sensitive", problemDetails.Detail, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class CapturingProblemDetailsService : IProblemDetailsService
    {
        public ProblemDetailsContext? Written { get; private set; }

        public ValueTask WriteAsync(ProblemDetailsContext context)
        {
            Written = context;
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> TryWriteAsync(ProblemDetailsContext context)
        {
            Written = context;
            return ValueTask.FromResult(true);
        }
    }
}
