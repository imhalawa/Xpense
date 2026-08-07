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

namespace Xpense.API.Features.Accounts;

public sealed class ListAccounts : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/accounts", Handle).WithName(nameof(ListAccounts));

    private static async Task<Ok<AccountResponse[]>> Handle(XpenseDbContext dbContext, CancellationToken cancellationToken)
    {
        var accounts = await dbContext.Accounts.AsNoTracking().ToListAsync(cancellationToken);
        return TypedResults.Ok(accounts.Select(AccountResponse.Of).ToArray());
    }
}
