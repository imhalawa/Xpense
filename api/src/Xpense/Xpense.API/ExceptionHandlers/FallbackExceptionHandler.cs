using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Serilog;

namespace Xpense.API.ExceptionHandlers;

public sealed class FallbackExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger logger) : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        logger.Error(exception, "Unhandled exception handling {Method} {Path}",
            httpContext.Request.Method, httpContext.Request.Path);

        return ProblemDetailsWriter.Write(
            problemDetailsService,
            httpContext,
            exception,
            StatusCodes.Status500InternalServerError,
            "Internal Server Error",
            "Internal Server Error, please check the logs or contact the administrator!");
    }
}
