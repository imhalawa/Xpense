using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xpense.API.Infrastructure;
using Xpense.Domain.Exceptions;
using Xpense.Persistence;

namespace Xpense.API.Features.Budgets;

public sealed class DeleteBudget : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/v1/budgets/{id:int}", Handle).WithName(nameof(DeleteBudget));

    private static async Task<NoContent> Handle(int id, XpenseDbContext dbContext, CancellationToken cancellationToken)
    {
        var budget = await dbContext.Budgets.FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
                     ?? throw new BudgetNotFoundException(id);

        budget.MarkAsDeleted();
        budget.Touch();

        if (await dbContext.SaveChangesAsync(cancellationToken) < 1)
            throw new BudgetDeletionFailedException(id);

        return TypedResults.NoContent();
    }
}
