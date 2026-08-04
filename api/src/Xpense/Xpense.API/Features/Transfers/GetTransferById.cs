using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xpense.API.Infrastructure;
using Xpense.Domain.Exceptions;
using Xpense.Persistence;

namespace Xpense.API.Features.Transfers;

/// <summary>
/// Added so CreateTransfer has somewhere to point its Location header. The spec requires
/// creates to return one, and until this existed the transfer endpoint could not comply.
/// </summary>
public sealed class GetTransferById : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/transfers/{id:int}", Handle).WithName(nameof(GetTransferById));

    private static async Task<Ok<TransferResponse>> Handle(int id, XpenseDbContext db, CancellationToken ct)
    {
        var transfer = await db.Transfers
            .AsNoTracking()
            .Include(item => item.Legs)
            .FirstOrDefaultAsync(item => item.Id == id, ct)
            ?? throw new TransferNotFoundException(id);

        return TypedResults.Ok(TransferResponse.Of(transfer));
    }
}
