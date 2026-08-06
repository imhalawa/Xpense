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
using Xpense.Domain.Enums;
using Xpense.Domain.Exceptions;
using Xpense.Domain.ValueObjects;
using Xpense.Persistence;

namespace Xpense.API.Features.Budgets;

public sealed class UpdateBudget : IEndpoint
{
    public sealed record Request(
        MoneyRequest Amount,
        string Recurrence,
        DateOnly StartsOn,
        DateOnly? EndsOn);

    public sealed record MoneyRequest(long MinorUnits, string Currency);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
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
        }

        private static bool IsOneOff(string recurrence) =>
            RecurrenceParser.TryParse(recurrence, out var parsed) && parsed == Recurrence.None;
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPut("/api/v1/budgets/{id:int}", Handle).WithName(nameof(UpdateBudget)).Validated();

    private static async Task<Ok<BudgetResponse>> Handle(
        int id,
        Request request,
        XpenseDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var budget = await dbContext.Budgets
            .Include(item => item.Category)
            .ThenInclude(category => category!.Priority)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new BudgetNotFoundException(id);

        CurrencyParser.TryParse(request.Amount.Currency, out var currency);
        RecurrenceParser.TryParse(request.Recurrence, out var recurrence);

        budget.Restate(
            Money.OfMinorUnits(request.Amount.MinorUnits, currency),
            recurrence,
            request.StartsOn.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            request.EndsOn?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

        if (await dbContext.SaveChangesAsync(cancellationToken) < 1)
            throw new BudgetUpdateFailedException(id);

        return TypedResults.Ok(BudgetResponse.Of(budget, null));
    }
}
