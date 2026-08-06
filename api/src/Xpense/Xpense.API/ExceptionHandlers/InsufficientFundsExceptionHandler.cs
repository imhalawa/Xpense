using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xpense.Domain.Exceptions;

namespace Xpense.API.ExceptionHandlers;

public sealed class InsufficientFundsExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not InsufficientFundsForTransferException insufficientFunds)
            return false;

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        var problemDetails = new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            ["amount.minorUnits"] = [insufficientFunds.Message]
        })
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Insufficient funds",
            Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}"
        };

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = insufficientFunds,
            ProblemDetails = problemDetails
        });
    }
}
