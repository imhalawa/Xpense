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
using Xpense.Domain.Exceptions;

namespace Xpense.API.Features.Categories;

public sealed class GetCategoryById : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/categories/{id:int}", Handle).WithName(nameof(GetCategoryById));

    private static async Task<Ok<CategoryResponse>> Handle(int id, XpenseDbContext dbContext, CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories
            .AsNoTracking()
            .Include(item => item.Priority)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new CategoryNotFoundException(id);

        return TypedResults.Ok(CategoryResponse.Of(category));
    }
}
