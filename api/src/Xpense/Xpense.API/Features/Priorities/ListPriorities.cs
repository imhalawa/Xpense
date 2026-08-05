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

/// <summary>
/// The priorities a category can be given. Read-only: they are reference data, seeded by a migration
/// rather than created through the API.
/// <para>
/// This exists because a category requires a priority id and nothing exposed the ids. A client had
/// to hardcode 1 to 5 and hope the seed never changed.
/// </para>
/// </summary>
public sealed class ListPriorities : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/priorities", Handle).WithName(nameof(ListPriorities));

    private static async Task<Ok<PriorityResponse[]>> Handle(XpenseDbContext db, CancellationToken ct)
    {
        // By id, which is seed order: Extreme, High, Medium, Low, None. Weight would put None first,
        // because None weighs 0 and Extreme weighs 1.
        var priorities = await db.Priorities
            .AsNoTracking()
            .OrderBy(priority => priority.Id)
            .ToListAsync(ct);

        return TypedResults.Ok(priorities.Select(PriorityResponse.Of).ToArray());
    }
}
