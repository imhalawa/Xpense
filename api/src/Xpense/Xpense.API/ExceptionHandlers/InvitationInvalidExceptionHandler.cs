using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Xpense.Domain.Exceptions;

namespace Xpense.API.ExceptionHandlers;

public sealed class InvitationInvalidExceptionHandler : IExceptionHandler
{
    private static readonly NeutralInvitationProblem Problem = new(
        "https://tools.ietf.org/html/rfc9110#section-15.5.5",
        "Invitation no longer valid",
        StatusCodes.Status404NotFound,
        "This invitation is no longer valid.");

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not InvitationInvalidException)
            return false;

        httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
        await httpContext.Response.WriteAsJsonAsync(
            Problem,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);
        return true;
    }

    private sealed record NeutralInvitationProblem(string Type, string Title, int Status, string Detail);
}
