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
using Xpense.Domain.Entities;

namespace Xpense.API.Features.Merchants;

public sealed class ListMerchants : IEndpoint
{
    private const int DefaultLimit = 20;
    private const int MinimumLimit = 1;
    private const int MaximumLimit = 100;

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/merchants", Handle).WithName(nameof(ListMerchants));

    private static async Task<Ok<MerchantResponse[]>> Handle(
        XpenseDbContext dbContext,
        CancellationToken cancellationToken,
        string? search = null,
        int limit = DefaultLimit)
    {
        var query = dbContext.Merchants.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search}%";
            query = query.Where(merchant => EF.Functions.ILike(merchant.Label, pattern));
        }

        var merchants = await query
            .OrderBy(merchant => merchant.Label)
            .Take(Math.Clamp(limit, MinimumLimit, MaximumLimit))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(merchants.Select(MerchantResponse.Of).ToArray());
    }
}

public sealed record MerchantResponse(
    int Id,
    string Label,
    string CreatedAt,
    string? UpdatedAt)
{
    public static MerchantResponse Of(Merchant merchant) => new(
        merchant.Id,
        merchant.Label,
        Timestamps.Iso(merchant.CreatedAt),
        Timestamps.Iso(merchant.UpdatedAt));
}
