using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Xpense.Domain.Exceptions;

namespace Xpense.API.ExceptionHandlers;

public sealed class ResourceGrantAlreadyActiveExceptionHandler(IProblemDetailsService problemDetailsService)
    : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not ResourceGrantAlreadyActiveException conflict)
            return ValueTask.FromResult(false);

        return ProblemDetailsWriter.Write(
            problemDetailsService,
            httpContext,
            conflict,
            StatusCodes.Status409Conflict,
            "Resource grant already active",
            "This group already has an active grant for the resource.",
            "ResourceGrantAlreadyActive");
    }
}
