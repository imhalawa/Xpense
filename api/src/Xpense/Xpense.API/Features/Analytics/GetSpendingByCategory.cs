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

        if (expensesToday.Count == 0)
            return TypedResults.Ok(new SpendingByCategoryResponse([], new MoneyResponse(0, nameof(Currency.EUR))));

        var currency = expensesToday[0].Currency.ToString();

        var byCategory = expensesToday
            .GroupBy(transaction => transaction.Category!)
            .Select(group => new CategorySpendingResponse(
                group.Key.Id,
                CategoryResponse.Of(group.Key),
                new MoneyResponse(group.Sum(transaction => transaction.AmountMinorUnits), currency)))
            .ToArray();

        var total = new MoneyResponse(expensesToday.Sum(transaction => transaction.AmountMinorUnits), currency);

        return TypedResults.Ok(new SpendingByCategoryResponse(byCategory, total));
    }
}

public sealed record SpendingByCategoryResponse(
    CategorySpendingResponse[] Expenses,
    MoneyResponse Total);

public sealed record CategorySpendingResponse(
    int Id,
    CategoryResponse Category,
    MoneyResponse Amount);
