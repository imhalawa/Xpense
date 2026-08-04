using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Xpense.Services.Exceptions;

namespace Xpense.API.ExceptionHandlers;

/// <summary>Handles <see cref="NotFoundException"/>. A resource was looked up by identity and does not exist.</summary>
public sealed class NotFoundExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not NotFoundException notFound)
            return ValueTask.FromResult(false);

        return ProblemDetailsWriter.Write(
            problemDetailsService,
            httpContext,
            notFound,
            StatusCodes.Status404NotFound,
            "Resource not found",
            notFound.Message);
    }
}
