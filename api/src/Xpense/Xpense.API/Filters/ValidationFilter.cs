using System.Collections.Generic;
using System.Threading.Tasks;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Xpense.API.Filters;

/// <summary>
/// Runs the registered FluentValidation validator for every action argument that has one and
/// throws on failure, so validation errors take the same path through the exception handlers
/// as domain errors. Arguments with no registered validator pass through untouched.
/// </summary>
public sealed class ValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var failures = new List<ValidationFailure>();

        foreach (var argument in context.ActionArguments.Values)
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
                failures.AddRange(result.Errors);
        }

        if (failures.Count > 0)
            throw new ValidationException(failures);

        await next();
    }
}
