using System;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Xpense.API.Infrastructure;

/// <summary>
/// Runs the registered FluentValidation validator for any argument that has one and throws on
/// failure, so validation errors reach the client through the same ValidationExceptionHandler
/// as before. Arguments without a validator pass through untouched.
/// </summary>
public sealed class ValidationEndpointFilter : IEndpointFilter
{
    public async ValueTask<object> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        foreach (var argument in context.Arguments)
        {
            if (argument is null)
                continue;

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator)
                continue;

            var result = await validator.ValidateAsync(
                new ValidationContext<object>(argument),
                context.HttpContext.RequestAborted);

            if (!result.IsValid)
                throw new ValidationException(result.Errors);
        }

        return await next(context);
    }
}

public static class ValidationFilterExtensions
{
    /// <summary>Opt a slice into request validation.</summary>
    public static RouteHandlerBuilder Validated(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter<ValidationEndpointFilter>();
}
