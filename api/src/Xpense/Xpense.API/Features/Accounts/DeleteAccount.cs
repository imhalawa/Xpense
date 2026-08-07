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

namespace Xpense.API.Features.Accounts;

public sealed class DeleteAccount : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/v1/accounts/{accountNumber}", Handle).WithName(nameof(DeleteAccount));

    private static async Task<NoContent> Handle(string accountNumber, XpenseDbContext dbContext, CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts.FirstOrDefaultAsync(account => account.AccountNumber == accountNumber, cancellationToken)
                      ?? throw new AccountNotFoundException(accountNumber);

        account.MarkAsDeleted();
        account.Touch();

        if (await dbContext.SaveChangesAsync(cancellationToken) < 1)
            throw new AccountDeletionFailedException(account.Id);

        return TypedResults.NoContent();
    }
}
