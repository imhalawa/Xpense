using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xpense.API.Features.Categories;
using Xpense.API.Infrastructure;
using Xpense.Persistence;
using Xpense.Services.Enums;

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

        var transactions = await db.Transactions
            .AsNoTracking()
            .Include(transaction => transaction.Category)
            .ThenInclude(category => category.Priority)
            .Where(transaction => transaction.CreatedOn.Date == today)
            .ToListAsync(ct);

        if (transactions.Count == 0)
            return TypedResults.Ok(new SpendingByCategoryResponse([], new MoneyResponse(0, nameof(Currency.EUR))));

        var currency = transactions[0].Currency.ToString();

        var expenses = transactions
            .GroupBy(transaction => transaction.Category)
            .Select(group => new CategorySpendingResponse(
                group.Key.Id,
                CategoryResponse.Of(group.Key),
                new MoneyResponse(group.Sum(transaction => transaction.Amount), currency)))
            .ToArray();

        var total = new MoneyResponse(transactions.Sum(transaction => transaction.Amount), currency);

        return TypedResults.Ok(new SpendingByCategoryResponse(expenses, total));
    }
}

public sealed record SpendingByCategoryResponse(
    CategorySpendingResponse[] Expenses,
    MoneyResponse Total);

public sealed record CategorySpendingResponse(
    int Id,
    CategoryResponse Category,
    MoneyResponse Amount);

public sealed record MoneyResponse(long Cents, string Currency);
