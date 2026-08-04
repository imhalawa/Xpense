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
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;
using Xpense.Domain.Exceptions;
using Xpense.Persistence;

namespace Xpense.API.Features.Accounts;

public sealed class CreateAccount : IEndpoint
{
    /// <summary>Account number allocated to the very first account.</summary>
    private const long FirstAccountNumber = 1_000_000_000;

    /// <summary>
    /// The opening balance carries a currency, and that currency denominates the account for
    /// its whole life. There is no way to create an account without saying what it holds.
    /// </summary>
    public sealed record Request(string Name, MoneyRequest Balance);

    public sealed record MoneyRequest(long Cents, string Currency);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Name)
                .NotEmpty().WithMessage("The name is required.")
                .MaximumLength(200);

            RuleFor(request => request.Balance)
                .NotNull().WithMessage("The balance is required.");

            When(request => request.Balance is not null, () =>
            {
                RuleFor(request => request.Balance.Cents)
                    .GreaterThanOrEqualTo(0).WithMessage("The opening balance cannot be negative.");

                RuleFor(request => request.Balance.Currency)
                    .Must(currency => CurrencyParser.TryParse(currency, out _))
                    .WithMessage("The currency must be a supported currency name.");
            });
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
        CurrencyParser.TryParse(request.Balance.Currency, out var currency);

        var account = new Account
        {
            Name = request.Name,
            BalanceCents = request.Balance.Cents,
            Currency = currency,
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
