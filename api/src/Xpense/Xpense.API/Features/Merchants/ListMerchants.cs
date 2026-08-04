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
using Xpense.Domain.Entities;

namespace Xpense.API.Features.Merchants;

public sealed class ListMerchants : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/merchants", Handle).WithName(nameof(ListMerchants));

    private static async Task<Ok<MerchantResponse[]>> Handle(XpenseDbContext db, CancellationToken ct)
    {
        var merchants = await db.Merchants.AsNoTracking().ToListAsync(ct);
        return TypedResults.Ok(merchants.Select(MerchantResponse.Of).ToArray());
    }
}

public sealed record MerchantResponse(
    int Id,
    string Label,
    long? CreatedOn,
    long? LastUpdated)
{
    public static MerchantResponse Of(Merchant merchant) => new(
        merchant.Id,
        merchant.Label,
        new DateTimeOffset(merchant.CreatedOn).ToUnixTimeSeconds(),
        merchant.LastUpdated.HasValue
            ? new DateTimeOffset(merchant.LastUpdated.Value).ToUnixTimeSeconds()
            : null);
}
