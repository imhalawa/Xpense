using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xpense.API.Contracts;
using Xpense.API.Infrastructure;
using Xpense.Persistence;
using Xpense.Domain.Enums;

namespace Xpense.API.Features.Analytics;

public sealed class GetSpendingByCategory : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/analytics/spending/by-category", Handle)
            .WithName(nameof(GetSpendingByCategory));

    private static async Task<Ok<SpendingByCategoryResponse>> Handle(
        XpenseDbContext db,
        CancellationToken ct)
    {
        // Timestamps are stored UTC, so "today" is a UTC day.
        var today = DateTime.UtcNow.Date;

        // Expenses only, and the filter has to be expressed as columns because Kind is computed.
        // Transfers would have no category to group by, and income is not spending -- it used to be
        // counted here, because nothing filtered by direction at all.
        var expensesToday = await db.Transactions
            .AsNoTracking()
            .Include(transaction => transaction.Category)
            .ThenInclude(category => category.Priority)
            .Where(transaction => transaction.SourceAccountId != null
                                  && transaction.DestinationAccountId == null
                                  && transaction.OccurredAt.Date == today)
            .ToListAsync(ct);

        // Grouped by currency as well as by category, because nothing converts. This used to label
        // the whole total with expensesToday[0].Currency and sum minor units across every currency
        // in the day, so 10 EUR and 10 USD reported as 2000 EUR -- money invented by addition.
        var byCategory = expensesToday
            .GroupBy(transaction => new { transaction.Category!.Id, transaction.Currency })
            .Select(group => new CategorySpendingResponse(
                group.Key.Id,
                CategoryResponse.Of(group.First().Category!),
                new MoneyResponse(group.Sum(transaction => transaction.AmountMinorUnits), group.Key.Currency.ToString())))
            .OrderBy(spending => spending.Id)
            .ThenBy(spending => spending.Amount.Currency)
            .ToArray();

        // One total per currency present. An empty day has no currency to speak for, so it has no
        // totals rather than a zero in a currency nobody used.
        var totals = expensesToday
            .GroupBy(transaction => transaction.Currency)
            .Select(group => new MoneyResponse(group.Sum(transaction => transaction.AmountMinorUnits), group.Key.ToString()))
            .OrderBy(total => total.Currency)
            .ToArray();

        return TypedResults.Ok(new SpendingByCategoryResponse(byCategory, totals));
    }
}

/// <summary>
/// <paramref name="Totals"/> is one entry per currency spent today, never a single summed figure:
/// adding amounts in different currencies produces a number that is true of nothing.
/// </summary>
public sealed record SpendingByCategoryResponse(
    CategorySpendingResponse[] Expenses,
    MoneyResponse[] Totals);

/// <summary>
/// One category's spending in one currency. A category spent in two currencies appears twice.
/// </summary>
public sealed record CategorySpendingResponse(
    int Id,
    CategoryResponse Category,
    MoneyResponse Amount);
