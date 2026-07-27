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

public sealed class GetAccountById : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/accounts/{id:int}", Handle).WithName(nameof(GetAccountById));

    private static async Task<Ok<AccountResponse>> Handle(int id, XpenseDbContext db, CancellationToken ct)
    {
        var account = await db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct)
                      ?? throw new AccountNotFoundException(id);

        return TypedResults.Ok(AccountResponse.Of(account));
    }
}
