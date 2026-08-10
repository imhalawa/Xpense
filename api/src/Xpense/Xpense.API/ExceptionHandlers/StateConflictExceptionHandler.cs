using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Xpense.Domain.Exceptions;

namespace Xpense.API.ExceptionHandlers;

public sealed class StateConflictExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken) => exception switch
    {
        InvitationStateConflictException conflict => ProblemDetailsWriter.Write(
            problemDetailsService,
            httpContext,
            conflict,
            StatusCodes.Status409Conflict,
            "Invitation state conflict",
            "This invitation has already been used by this account.",
            "InvitationStateConflict"),
        LastVaultWrapperException conflict => ProblemDetailsWriter.Write(
            problemDetailsService,
            httpContext,
            conflict,
            StatusCodes.Status409Conflict,
            "Vault recovery method required",
            conflict.Message,
            "LastVaultWrapper"),
        _ => ValueTask.FromResult(false)
    };
}
