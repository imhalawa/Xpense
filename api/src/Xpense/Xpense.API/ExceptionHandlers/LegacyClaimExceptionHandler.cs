using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Xpense.Domain.Exceptions;

namespace Xpense.API.ExceptionHandlers;

public sealed class LegacyClaimExceptionHandler(IProblemDetailsService problemDetailsService)
    : IExceptionHandler
{
    private static readonly NeutralLegacyClaimProblem NeutralProblem = new(
        "https://tools.ietf.org/html/rfc9110#section-15.5.5",
        "Legacy claim unavailable",
        StatusCodes.Status404NotFound,
        "The legacy claim is unavailable.");

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is LegacyClaimInvalidException)
        {
            httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            await httpContext.Response.WriteAsJsonAsync(
                NeutralProblem,
                options: null,
                contentType: "application/problem+json",
                cancellationToken: cancellationToken);
            return true;
        }

        if (exception is LegacyClaimVerificationFailedException verificationFailed)
            return await ProblemDetailsWriter.Write(
                problemDetailsService,
                httpContext,
                verificationFailed,
                StatusCodes.Status409Conflict,
                "Legacy claim verification failed",
                "The encrypted records do not match the legacy dataset.",
                "LegacyClaimVerificationFailed");

        if (exception is LegacyClaimAlreadyCompletedException alreadyCompleted)
            return await ProblemDetailsWriter.Write(
                problemDetailsService,
                httpContext,
                alreadyCompleted,
                StatusCodes.Status409Conflict,
                "Legacy claim already completed",
                "The installation's legacy data was already claimed.",
                "LegacyClaimAlreadyCompleted");

        if (exception is LegacyClaimInProgressException inProgress)
            return await ProblemDetailsWriter.Write(
                problemDetailsService,
                httpContext,
                inProgress,
                StatusCodes.Status409Conflict,
                "Legacy claim in progress",
                "Legacy financial data is read-only until the claim completes.",
                "LegacyClaimInProgress");

        return false;
    }

    private sealed record NeutralLegacyClaimProblem(string Type, string Title, int Status, string Detail);
}
