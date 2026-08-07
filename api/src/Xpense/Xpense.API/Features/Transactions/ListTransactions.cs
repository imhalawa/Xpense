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
using Xpense.Persistence;
using Xpense.Domain.Exceptions;

namespace Xpense.API.Features.Transactions;

public sealed class ListTransactions : IEndpoint
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 25;

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/transactions", Handle)
            .WithName(nameof(ListTransactions))
            .WithDescription(
                "Lists transactions newest first. The optional from and to parameters bound "
                + "occurredAt as a half-open range: from is included, to is excluded, so a single "
                + "day is from=2026-07-26T00:00:00Z&to=2026-07-27T00:00:00Z.");

    private static async Task<Ok<TransactionPageResponse>> Handle(
        XpenseDbContext dbContext,
        CancellationToken cancellationToken,
        int page = DefaultPage,
        int pageSize = DefaultPageSize,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null)
    {
        if (page <= 0 || pageSize <= 0)
            throw new InvalidFilteredResultParams(page, pageSize);

        if (from is not null && to is not null && from >= to)
            throw new InvalidFilteredResultRange(from.Value, to.Value);

        var query = dbContext.WithDetails().AsNoTracking();

        if (from is not null)
        {
            var start = from.Value.UtcDateTime;
            query = query.Where(transaction => transaction.OccurredAt >= start);
        }

        if (to is not null)
        {
            var end = to.Value.UtcDateTime;
            query = query.Where(transaction => transaction.OccurredAt < end);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = totalItems / pageSize + (totalItems % pageSize > 0 ? 1 : 0);

        var transactions = await query
            .OrderByDescending(transaction => transaction.OccurredAt)
            .Skip(pageSize * (page - 1))
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new TransactionPageResponse(
            transactions.Select(TransactionResponse.Of).ToArray(),
            page,
            pageSize,
            totalItems,
            totalPages));
    }
}
