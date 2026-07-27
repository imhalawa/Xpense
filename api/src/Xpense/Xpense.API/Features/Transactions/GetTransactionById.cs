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

public sealed class GetTransactionById : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/transactions/{id:int}", Handle).WithName(nameof(GetTransactionById));

    private static async Task<Ok<TransactionResponse>> Handle(int id, XpenseDbContext db, CancellationToken ct)
    {
        var transaction = await db.WithDetails()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, ct)
            ?? throw new TransactionNotFoundException(id);

        return TypedResults.Ok(TransactionResponse.Of(transaction));
    }
}
