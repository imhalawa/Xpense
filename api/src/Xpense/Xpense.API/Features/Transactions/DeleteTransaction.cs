using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xpense.API.Infrastructure;
using Xpense.API.Infrastructure.LegacyClaim;
using Xpense.Domain.Exceptions;
using Xpense.Persistence;

namespace Xpense.API.Features.Transactions;

public sealed class DeleteTransaction : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/v1/transactions/{id:int}", Handle).WithName(nameof(DeleteTransaction)).BlocksDuringLegacyClaim();

    private static async Task<NoContent> Handle(
        int id,
        XpenseDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.WithDetails()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new TransactionNotFoundException(id);

        await using var scope = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        transaction.ReverseBalanceEffect();
        transaction.MarkAsDeleted();
        transaction.Touch();

        if (await dbContext.SaveChangesAsync(cancellationToken) < 1)
            throw new TransactionDeletionFailedException(id);

        await scope.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
