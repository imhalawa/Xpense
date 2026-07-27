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

namespace Xpense.API.Features.Tags;

public sealed class GetTagById : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/tags/{id:int}", Handle).WithName(nameof(GetTagById));

    private static async Task<Ok<TagResponse>> Handle(int id, XpenseDbContext db, CancellationToken ct)
    {
        var tag = await db.Tags.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct)
                  ?? throw new TagNotFoundException(id);

        return TypedResults.Ok(TagResponse.Of(tag));
    }
}
