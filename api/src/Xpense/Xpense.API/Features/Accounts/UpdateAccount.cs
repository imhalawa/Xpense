using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xpense.API.Infrastructure;
using Xpense.Persistence;
using Xpense.Domain.Exceptions;

namespace Xpense.API.Features.Accounts;

public sealed class UpdateAccount : IEndpoint
{
    public sealed record Request(string Label, bool IsDefault);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Label)
                .NotEmpty().WithMessage("The label is required.")
                .MaximumLength(200);
        }
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPut("/api/v1/accounts/{accountNumber}", Handle).WithName(nameof(UpdateAccount)).Validated();

    private static async Task<Ok<AccountResponse>> Handle(
        string accountNumber,
        Request request,
        XpenseDbContext db,
        CancellationToken ct)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.AccountNumber == accountNumber, ct)
                      ?? throw new AccountNotFoundException(accountNumber);

        account.Label = request.Label;
        account.IsDefault = request.IsDefault;
        account.Touch();

        if (await db.SaveChangesAsync(ct) < 1)
            throw new AccountUpdateFailedException(account.Id);

        return TypedResults.Ok(AccountResponse.Of(account));
    }
}
