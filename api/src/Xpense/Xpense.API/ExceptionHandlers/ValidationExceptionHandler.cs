using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Xpense.API.ExceptionHandlers;

/// <summary>
/// Handles FluentValidation's <see cref="ValidationException"/>, thrown by
/// <see cref="Infrastructure.ValidationEndpointFilter"/> when a request DTO fails its validator.
/// </summary>
public sealed class ValidationExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ValidationException validationException)
            return false;

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        var errors = validationException.Errors
            .GroupBy(failure => ProblemDetailsWriter.ToCamelCasePath(failure.PropertyName))
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).Distinct().ToArray());

        var problemDetails = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred.",
            Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}"
        };

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = validationException,
            ProblemDetails = problemDetails
        });
    }
}
