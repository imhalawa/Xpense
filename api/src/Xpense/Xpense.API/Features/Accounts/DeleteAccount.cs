using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xpense.API.Infrastructure;
using Xpense.Persistence;
using Xpense.Services.Exceptions;

namespace Xpense.API.Features.Accounts;

public sealed class DeleteAccount : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/v1/accounts/{id:int}", Handle).WithName(nameof(DeleteAccount));

    private static async Task<NoContent> Handle(int id, XpenseDbContext db, CancellationToken ct)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == id, ct)
                      ?? throw new AccountNotFoundException(id);

        account.MarkAsDeleted();
        account.Touch();

        if (await db.SaveChangesAsync(ct) < 1)
            throw new AccountDeletionFailedException(id);

        return TypedResults.NoContent();
    }
}
