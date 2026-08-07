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
        XpenseDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;

        var expensesToday = await dbContext.Transactions
            .AsNoTracking()
            .Include(transaction => transaction.Category)
            .ThenInclude(category => category.Priority)
            .Where(transaction => transaction.SourceAccountId != null
                                  && transaction.DestinationAccountId == null
                                  && transaction.OccurredAt.Date == today)
            .ToListAsync(cancellationToken);

        var byCategory = expensesToday
            .GroupBy(transaction => new { transaction.Category!.Id, transaction.Currency })
            .Select(group => new CategorySpendingResponse(
                group.Key.Id,
                CategoryResponse.Of(group.First().Category!),
                new MoneyResponse(group.Sum(transaction => transaction.AmountMinorUnits), group.Key.Currency.ToString())))
            .OrderBy(spending => spending.Id)
            .ThenBy(spending => spending.Amount.Currency)
            .ToArray();

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
