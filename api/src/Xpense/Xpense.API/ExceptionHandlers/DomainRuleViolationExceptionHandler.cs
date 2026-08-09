using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Xpense.Domain.Exceptions;

namespace Xpense.API.ExceptionHandlers;

public sealed class DomainRuleViolationExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DomainRuleViolationException violation)
            return ValueTask.FromResult(false);

        if (violation is LastVaultWrapperException)
            return ProblemDetailsWriter.Write(
                problemDetailsService,
                httpContext,
                violation,
                StatusCodes.Status409Conflict,
                "Vault recovery method required",
                violation.Message,
                "LastVaultWrapper");

        return ProblemDetailsWriter.Write(
            problemDetailsService,
            httpContext,
            violation,
            StatusCodes.Status400BadRequest,
            "Request breaks a domain rule",
            violation.Message);
    }
}
