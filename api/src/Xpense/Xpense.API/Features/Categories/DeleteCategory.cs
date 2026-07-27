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

namespace Xpense.API.Features.Categories;

public sealed class DeleteCategory : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/v1/categories/{id:int}", Handle).WithName(nameof(DeleteCategory));

    private static async Task<NoContent> Handle(int id, XpenseDbContext db, CancellationToken ct)
    {
        // CategoryRepository.DeleteById wrapped a FirstAsync in catch-all and rethrew as
        // CategoryNotFoundException. Same outcome, without swallowing unrelated failures.
        var category = await db.Categories.FirstOrDefaultAsync(item => item.Id == id, ct)
                       ?? throw new CategoryNotFoundException(id);

        category.MarkAsDeleted();
        category.Touch();

        if (await db.SaveChangesAsync(ct) < 1)
            throw new CategoryDeletionFailedException(id);

        return TypedResults.NoContent();
    }
}
