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

/// <summary>
/// One budget and what it has measured over one of its periods.
/// <para>
/// Unlike <see cref="ListBudgets"/>, the totals are summed in the database and grouped by currency
/// there: with a single budget there is one window to ask about, so nothing has to be sorted out in
/// memory afterwards.
/// </para>
/// </summary>
public sealed class GetBudgetById : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/budgets/{id:int}", Handle).WithName(nameof(GetBudgetById));

    private static async Task<Ok<BudgetResponse>> Handle(
        int id,
        XpenseDbContext db,
        CancellationToken ct,
        DateTimeOffset? on = null)
    {
        var instant = on?.UtcDateTime ?? DateTime.UtcNow;

        var budget = await db.Budgets
            .AsNoTracking()
            .Include(item => item.Category)
            .ThenInclude(category => category!.Priority)
            .FirstOrDefaultAsync(item => item.Id == id, ct)
            ?? throw new BudgetNotFoundException(id);

        var period = budget.PeriodOn(instant);

        if (period is null)
            return TypedResults.Ok(BudgetResponse.Of(budget, null));

        var totals = await db.Transactions
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
            .ToListAsync(ct);

        // Built in the budget's own currency: Money.Zero is EUR, and a EUR zero taken from a USD
        // limit throws instead of returning the limit untouched.
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
