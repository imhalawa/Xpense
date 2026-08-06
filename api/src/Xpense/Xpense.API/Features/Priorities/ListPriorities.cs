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

namespace Xpense.API.Features.Priorities;

public sealed class ListPriorities : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/priorities", Handle).WithName(nameof(ListPriorities));

    private static async Task<Ok<PriorityResponse[]>> Handle(XpenseDbContext dbContext, CancellationToken cancellationToken)
    {
        var priorities = await dbContext.Priorities
            .AsNoTracking()
            .OrderBy(priority => priority.Id)
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(priorities.Select(PriorityResponse.Of).ToArray());
    }
}
