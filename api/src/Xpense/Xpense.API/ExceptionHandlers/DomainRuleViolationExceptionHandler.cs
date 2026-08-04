using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Xpense.Services.Exceptions;

namespace Xpense.API.ExceptionHandlers;

/// <summary>
/// Handles <see cref="DomainRuleViolationException"/>. The request was well-formed but breaks a
/// domain rule, so the caller can fix it by changing the request.
/// </summary>
public sealed class DomainRuleViolationExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DomainRuleViolationException violation)
            return ValueTask.FromResult(false);

        return ProblemDetailsWriter.Write(
            problemDetailsService,
            httpContext,
            violation,
            StatusCodes.Status400BadRequest,
            "Request breaks a domain rule",
            violation.Message);
    }
}
