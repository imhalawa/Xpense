using System;
using System.Linq;
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
using Xpense.Services.Entities;
using Xpense.Services.Exceptions;

namespace Xpense.API.Features.Accounts;

public sealed class CreateAccount : IEndpoint
{
    /// <summary>Account number allocated to the very first account.</summary>
    private const long FirstAccountNumber = 1_000_000_000;

    public sealed record Request(string Name, decimal Balance);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Name)
                .NotEmpty().WithMessage("The name is required.")
                .MaximumLength(200);

            RuleFor(request => request.Balance)
                .GreaterThanOrEqualTo(0).WithMessage("The opening balance cannot be negative.");
        }
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/accounts", Handle).WithName(nameof(CreateAccount)).Validated();

    private static async Task<Created<AccountResponse>> Handle(
        Request request,
        XpenseDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        var account = new Account
        {
            Name = request.Name,
            Balance = request.Balance,
            AccountNumber = await NextAccountNumber(db, ct),
            IsDefaultAccount = !await db.Accounts.AnyAsync(a => a.IsDefaultAccount, ct),
            CreatedOn = DateTime.UtcNow
        };

        db.Accounts.Add(account);

        if (await db.SaveChangesAsync(ct) < 1)
            throw new AccountCreationFailedException(request.Name);

        return TypedResults.Created(
            http.ResourceUri($"/api/v1/accounts/{account.Id}"),
            AccountResponse.Of(account));
    }

    /// <summary>
    /// AccountRepository.GetNextAccountNumber called Max() on the parsed numbers, which threw
    /// InvalidOperationException on an empty table -- creating the very first account crashed.
    /// Tests never caught it because they always seeded an account first.
    /// </summary>
    private static async Task<string> NextAccountNumber(XpenseDbContext db, CancellationToken ct)
    {
        var numbers = await db.Accounts.Select(a => a.AccountNumber).ToListAsync(ct);

        return numbers.Count == 0
            ? FirstAccountNumber.ToString()
            : (numbers.Max(long.Parse) + 1).ToString();
    }
}
