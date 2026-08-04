using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Serilog;
using Xpense.Services.Exceptions;

namespace Xpense.API.ExceptionHandlers;

/// <summary>
/// Handles <see cref="PersistenceFailedException"/>. A write we expected to succeed did not.
/// The caller cannot fix this, so the detail stays generic and the cause goes to the log.
/// </summary>
public sealed class PersistenceFailedExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger logger) : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not PersistenceFailedException persistenceFailed)
            return ValueTask.FromResult(false);

        logger.Error(persistenceFailed, "Persistence failed handling {Method} {Path}",
            httpContext.Request.Method, httpContext.Request.Path);

        return ProblemDetailsWriter.Write(
            problemDetailsService,
            httpContext,
            persistenceFailed,
            StatusCodes.Status500InternalServerError,
            "The change could not be saved",
            "The request was understood but the change could not be persisted. Please retry, or contact the administrator if this continues.");
    }
}
