using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Xpense.API.Infrastructure;
using Xpense.Persistence;

namespace Xpense.API.Features.Tags;

public sealed class ListTags : IEndpoint
{
    private const int DefaultLimit = 20;
    private const int MinimumLimit = 1;
    private const int MaximumLimit = 100;

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/tags", Handle).WithName(nameof(ListTags));

    private static async Task<Ok<TagResponse[]>> Handle(
        XpenseDbContext dbContext,
        CancellationToken cancellationToken,
        string? search = null,
        int limit = DefaultLimit)
    {
        var query = dbContext.Tags.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search}%";
            query = query.Where(tag => EF.Functions.ILike(tag.Label, pattern));
        }

        var tags = await query
            .OrderBy(tag => tag.Label)
            .Take(Math.Clamp(limit, MinimumLimit, MaximumLimit))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(tags.Select(TagResponse.Of).ToArray());
    }
}
