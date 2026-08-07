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

public sealed class GetAccountByNumber : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/accounts/{accountNumber}", Handle).WithName(nameof(GetAccountByNumber));

    private static async Task<Ok<AccountResponse>> Handle(
        string accountNumber,
        XpenseDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(account => account.AccountNumber == accountNumber, cancellationToken)
            ?? throw new AccountNotFoundException(accountNumber);

        return TypedResults.Ok(AccountResponse.Of(account));
    }
}
