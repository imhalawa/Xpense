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
        app.MapGet("/api/v1/transactions", Handle).WithName(nameof(ListTransactions));

    private static async Task<Ok<TransactionPageResponse>> Handle(
        XpenseDbContext db,
        CancellationToken ct,
        int page = DefaultPage,
        int pageSize = DefaultPageSize)
    {
        if (page <= 0 || pageSize <= 0)
            throw new InvalidFilteredResultParams(page, pageSize);

        var query = db.WithDetails().AsNoTracking();

        var totalItems = await query.CountAsync(ct);
        var totalPages = totalItems / pageSize + (totalItems % pageSize > 0 ? 1 : 0);

        var transactions = await query
            // Ordered by when the money moved, not when the row was written. Those were the same
            // column until OccurredAt became one of its own.
            .OrderByDescending(transaction => transaction.OccurredAt)
            .Skip(pageSize * (page - 1))
            .Take(pageSize)
            .ToListAsync(ct);

        return TypedResults.Ok(new TransactionPageResponse(
            transactions.Select(TransactionResponse.Of).ToArray(),
            page,
            pageSize,
            totalItems,
            totalPages));
    }
}
