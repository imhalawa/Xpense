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
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/tags", Handle).WithName(nameof(ListTags));

    private static async Task<Ok<TagResponse[]>> Handle(XpenseDbContext dbContext, CancellationToken cancellationToken)
    {
        var tags = await dbContext.Tags.AsNoTracking().ToListAsync(cancellationToken);
        return TypedResults.Ok(tags.Select(TagResponse.Of).ToArray());
    }
}
