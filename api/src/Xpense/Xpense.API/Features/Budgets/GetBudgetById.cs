using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xpense.API.Infrastructure;
using Xpense.Domain.Exceptions;
using Xpense.Domain.ValueObjects;
using Xpense.Persistence;

namespace Xpense.API.Features.Budgets;

public sealed class GetBudgetById : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/budgets/{id:int}", Handle).WithName(nameof(GetBudgetById));

    private static async Task<Ok<BudgetResponse>> Handle(
        int id,
        XpenseDbContext dbContext,
        CancellationToken cancellationToken,
        DateTimeOffset? on = null)
    {
        var instant = on?.UtcDateTime ?? DateTime.UtcNow;

        var budget = await dbContext.Budgets
            .AsNoTracking()
            .Include(item => item.Category)
            .ThenInclude(category => category!.Priority)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new BudgetNotFoundException(id);

        var period = budget.PeriodOn(instant);

        if (period is null)
            return TypedResults.Ok(BudgetResponse.Of(budget, null));

        var totals = await dbContext.Transactions
            .AsNoTracking()
            .Where(transaction => transaction.SourceAccountId != null
                                  && transaction.DestinationAccountId == null
                                  && transaction.CategoryId == budget.CategoryId
                                  && transaction.OccurredAt >= period.From
                                  && transaction.OccurredAt < period.ToExclusive)
            .GroupBy(transaction => transaction.Currency)
            .Select(group => new
            {
                Currency = group.Key,
                MinorUnits = group.Sum(transaction => transaction.AmountMinorUnits)
            })
            .ToListAsync(cancellationToken);

        var spent = Money.OfMinorUnits(
            totals.SingleOrDefault(total => total.Currency == budget.Currency)?.MinorUnits ?? 0,
            budget.Currency);

        var uncounted = totals
            .Where(total => total.Currency != budget.Currency)
            .OrderBy(total => total.Currency)
            .Select(total => Money.OfMinorUnits(total.MinorUnits, total.Currency))
            .ToArray();

        return TypedResults.Ok(
            BudgetResponse.Of(budget, BudgetPeriodResponse.Of(period, budget.Amount, spent, uncounted)));
    }
}
