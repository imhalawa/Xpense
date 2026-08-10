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

namespace Xpense.API.Features.Merchants;

public sealed class GetMerchantById : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/merchants/{id:int}", Handle).WithName(nameof(GetMerchantById));

    private static async Task<Ok<MerchantResponse>> Handle(
        int id,
        XpenseDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var merchant = await dbContext.Merchants
            .AsNoTracking()
            .SingleOrDefaultAsync(merchant => merchant.Id == id, cancellationToken)
            ?? throw new MerchantNotFoundException(id);

        return TypedResults.Ok(MerchantResponse.Of(merchant));
    }
}
