using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xpense.API.Infrastructure;
using Xpense.Domain.Enums;
using Xpense.Domain.ValueObjects;
using Xpense.Persistence;

namespace Xpense.API.Features.Budgets;

/// <summary>
/// Every budget with what it has measured so far, ready for a dashboard in one request.
/// <para>
/// Spending is fetched with a single query covering every budget in the list rather than one query
/// per budget, so ten budgets cost two round trips and not eleven. The window asked for spans the
/// earliest period start to the latest period end, which over-fetches when a weekly budget and a
/// yearly one appear together -- one wide filtered read beats N narrow ones.
/// </para>
/// </summary>
public sealed class ListBudgets : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/budgets", Handle).WithName(nameof(ListBudgets));

    private static async Task<Ok<BudgetResponse[]>> Handle(
        XpenseDbContext dbContext,
        CancellationToken cancellationToken,
        DateTimeOffset? on = null)
    {
        // Which period a budget is in depends on a moment. Default to now; `?on=` asks about another.
        var instant = on?.UtcDateTime ?? DateTime.UtcNow;

        var budgets = await dbContext.Budgets
            .AsNoTracking()
            .Include(budget => budget.Category)
            .ThenInclude(category => category!.Priority)
            .OrderBy(budget => budget.CategoryId)
            .ThenBy(budget => budget.Id)
            .ToListAsync(cancellationToken);

        var measured = budgets
            .Select(budget => (Budget: budget, Period: budget.PeriodOn(instant)))
            .ToArray();

        var active = measured.Where(entry => entry.Period is not null).ToArray();

        if (active.Length == 0)
            return TypedResults.Ok(measured.Select(entry => BudgetResponse.Of(entry.Budget, null)).ToArray());

        var categoryIds = active.Select(entry => entry.Budget.CategoryId).Distinct().ToArray();
        var from = active.Min(entry => entry.Period!.From);
        var toExclusive = active.Max(entry => entry.Period!.ToExclusive);

        // Expenses only, and the filter is written as columns because Kind is computed. A transfer
        // has no category to count against, and income is not spending.
        var expenses = await dbContext.Transactions
            .AsNoTracking()
            .Where(transaction => transaction.SourceAccountId != null
                                  && transaction.DestinationAccountId == null
                                  && transaction.CategoryId != null
                                  && categoryIds.Contains(transaction.CategoryId.Value)
                                  && transaction.OccurredAt >= from
                                  && transaction.OccurredAt < toExclusive)
            .Select(transaction => new Expense(
                transaction.CategoryId!.Value,
                transaction.Currency,
                transaction.OccurredAt,
                transaction.AmountMinorUnits))
            .ToListAsync(cancellationToken);

        var responses = measured
            .Select(entry => entry.Period is null
                ? BudgetResponse.Of(entry.Budget, null)
                : BudgetResponse.Of(entry.Budget, Measure(entry.Budget.Amount, entry.Budget.CategoryId, entry.Period, expenses)))
            .ToArray();

        return TypedResults.Ok(responses);
    }

    private static BudgetPeriodResponse Measure(
        Money limit,
        int categoryId,
        BudgetPeriod period,
        List<Expense> expenses)
    {
        var inPeriod = expenses
            .Where(expense => expense.CategoryId == categoryId && period.Contains(expense.OccurredAt))
            .ToArray();

        // Zero has to be built in this budget's currency. Money.Zero is EUR, and subtracting a EUR
        // zero from a USD limit throws rather than returning the limit.
        var spent = Money.OfMinorUnits(
            inPeriod.Where(expense => expense.Currency == limit.Currency).Sum(expense => expense.MinorUnits),
            limit.Currency);

        var uncounted = inPeriod
            .Where(expense => expense.Currency != limit.Currency)
            .GroupBy(expense => expense.Currency)
            .Select(group => Money.OfMinorUnits(group.Sum(expense => expense.MinorUnits), group.Key))
            .OrderBy(money => money.Currency)
            .ToArray();

        return BudgetPeriodResponse.Of(period, limit, spent, uncounted);
    }

    private sealed record Expense(int CategoryId, Currency Currency, DateTime OccurredAt, long MinorUnits);
}
