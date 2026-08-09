using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xpense.API.Infrastructure;
using Xpense.API.Infrastructure.LegacyClaim;
using Xpense.Domain.Exceptions;
using Xpense.Persistence;

namespace Xpense.API.Features.Merchants;

public sealed class DeleteMerchant : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/v1/merchants/{id:int}", Handle).WithName(nameof(DeleteMerchant)).BlocksDuringLegacyClaim();

    private static async Task<NoContent> Handle(
        int id,
        XpenseDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var merchant = await dbContext.Merchants.SingleOrDefaultAsync(
                           merchant => merchant.Id == id,
                           cancellationToken)
                       ?? throw new MerchantNotFoundException(id);

        merchant.MarkAsDeleted();
        merchant.Touch();

        if (await dbContext.SaveChangesAsync(cancellationToken) < 1)
            throw new MerchantDeletionFailedException(id);

        return TypedResults.NoContent();
    }
}
