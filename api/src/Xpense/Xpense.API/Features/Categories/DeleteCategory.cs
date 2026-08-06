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
using Xpense.Domain.Exceptions;

namespace Xpense.API.Features.Categories;

public sealed class DeleteCategory : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/v1/categories/{id:int}", Handle).WithName(nameof(DeleteCategory));

    private static async Task<NoContent> Handle(int id, XpenseDbContext dbContext, CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories.FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
                       ?? throw new CategoryNotFoundException(id);

        category.MarkAsDeleted();
        category.Touch();

        var budgets = await dbContext.Budgets.Where(budget => budget.CategoryId == id).ToListAsync(cancellationToken);

        foreach (var budget in budgets)
        {
            budget.MarkAsDeleted();
            budget.Touch();
        }

        if (await dbContext.SaveChangesAsync(cancellationToken) < 1)
            throw new CategoryDeletionFailedException(id);

        return TypedResults.NoContent();
    }
}
