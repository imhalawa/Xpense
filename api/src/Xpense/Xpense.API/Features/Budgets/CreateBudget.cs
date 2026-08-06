using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xpense.API.Infrastructure;
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;
using Xpense.Domain.Exceptions;
using Xpense.Domain.ValueObjects;
using Xpense.Persistence;

namespace Xpense.API.Features.Budgets;

public sealed class CreateBudget : IEndpoint
{
    public sealed record Request(
        int CategoryId,
        MoneyRequest Amount,
        string Recurrence,
        DateOnly StartsOn,
        DateOnly? EndsOn,
        int? AlertThresholdPercent = Budget.DefaultAlertThreshold);

    public sealed record MoneyRequest(long MinorUnits, string Currency);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.CategoryId)
                .GreaterThan(0).WithMessage("The category must be a valid selection.");

            RuleFor(request => request.Amount)
                .NotNull().WithMessage("The amount is required.");

            When(request => request.Amount is not null, () =>
            {
                RuleFor(request => request.Amount.MinorUnits)
                    .GreaterThan(0).WithMessage("The amount in minor units must be positive.");

                RuleFor(request => request.Amount.Currency)
                    .Must(currency => CurrencyParser.TryParse(currency, out _))
                    .WithMessage("The currency must be a supported currency name.");
            });

            RuleFor(request => request.Recurrence)
                .Must(recurrence => RecurrenceParser.TryParse(recurrence, out _))
                .WithMessage("The recurrence must be one of None, Weekly, Monthly or Yearly.");

            RuleFor(request => request.EndsOn)
                .NotNull()
                .When(request => IsOneOff(request.Recurrence))
                .WithMessage("A budget that does not repeat must state when it ends.");

            RuleFor(request => request.EndsOn)
                .Must((request, endsOn) => endsOn is null || endsOn >= request.StartsOn)
                .WithMessage("The end date cannot be before the start date.");

            RuleFor(request => request.AlertThresholdPercent)
                .InclusiveBetween(1, 100)
                .When(request => request.AlertThresholdPercent is not null)
                .WithMessage("The alert threshold must be between 1 and 100 percent.");
        }

        private static bool IsOneOff(string recurrence) =>
            RecurrenceParser.TryParse(recurrence, out var parsed) && parsed == Recurrence.None;
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/budgets", Handle).WithName(nameof(CreateBudget)).Validated();

    private static async Task<Created<BudgetResponse>> Handle(
        Request request,
        XpenseDbContext dbContext,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories
            .Include(item => item.Priority)
            .FirstOrDefaultAsync(item => item.Id == request.CategoryId, cancellationToken)
            ?? throw new CategoryNotFoundException(request.CategoryId);

        CurrencyParser.TryParse(request.Amount.Currency, out var currency);
        RecurrenceParser.TryParse(request.Recurrence, out var recurrence);

        var budget = Budget.For(
            category,
            Money.OfMinorUnits(request.Amount.MinorUnits, currency),
            recurrence,
            request.StartsOn.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            request.EndsOn?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            request.AlertThresholdPercent);

        dbContext.Budgets.Add(budget);

        if (await dbContext.SaveChangesAsync(cancellationToken) < 1)
            throw new BudgetCreationFailedException(request.CategoryId);

        return TypedResults.Created(
            httpContext.ResourceUri($"/api/v1/budgets/{budget.Id}"),
            BudgetResponse.Of(budget, null));
    }
}
