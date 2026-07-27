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

namespace Xpense.API.Features.Categories;

public sealed class ListCategories : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/categories", Handle).WithName(nameof(ListCategories));

    private static async Task<Ok<CategoryResponse[]>> Handle(XpenseDbContext db, CancellationToken ct)
    {
        var categories = await db.Categories
            .AsNoTracking()
            .Include(category => category.Priority)
            .ToListAsync(ct);

        return TypedResults.Ok(categories.Select(CategoryResponse.Of).ToArray());
    }
}
