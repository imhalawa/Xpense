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
using Xpense.Services.Exceptions;

namespace Xpense.API.Features.Accounts;

public sealed class UpdateAccount : IEndpoint
{
    public sealed record Request(string Name, bool IsDefault);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Name)
                .NotEmpty().WithMessage("The name is required.")
                .MaximumLength(200);
        }
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPut("/api/v1/accounts/{id:int}", Handle).WithName(nameof(UpdateAccount)).Validated();

    private static async Task<Ok<AccountResponse>> Handle(
        int id,
        Request request,
        XpenseDbContext db,
        CancellationToken ct)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == id, ct)
                      ?? throw new AccountNotFoundException(id);

        account.Name = request.Name;
        account.IsDefaultAccount = request.IsDefault;
        account.Touch();

        if (await db.SaveChangesAsync(ct) < 1)
            throw new AccountUpdateFailedException(id);

        return TypedResults.Ok(AccountResponse.Of(account));
    }
}
