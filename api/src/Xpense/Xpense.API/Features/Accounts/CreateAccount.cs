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
    private const long FirstAccountNumber = 1_000_000_000;

    /// <summary>
    /// The opening balance carries a currency, and that currency denominates the account for
    /// its whole life. There is no way to create an account without saying what it holds.
    /// </summary>
    public sealed record Request(string Label, MoneyRequest Balance);

    public sealed record MoneyRequest(long MinorUnits, string Currency);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Label)
                .NotEmpty().WithMessage("The label is required.")
                .MaximumLength(200);

            RuleFor(request => request.Balance)
                .NotNull().WithMessage("The balance is required.");

            When(request => request.Balance is not null, () =>
            {
                RuleFor(request => request.Balance.MinorUnits)
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
        XpenseDbContext dbContext,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        CurrencyParser.TryParse(request.Balance.Currency, out var currency);

        var account = new Account
        {
            Label = request.Label,
            BalanceMinorUnits = request.Balance.MinorUnits,
            Currency = currency,
            AccountNumber = await NextAccountNumber(dbContext, cancellationToken),
            IsDefault = !await dbContext.Accounts.AnyAsync(account => account.IsDefault, cancellationToken),
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Accounts.Add(account);

        if (await dbContext.SaveChangesAsync(cancellationToken) < 1)
            throw new AccountCreationFailedException(request.Label);

        return TypedResults.Created(
            httpContext.ResourceUri($"/api/v1/accounts/{account.AccountNumber}"),
            AccountResponse.Of(account));
    }

    private static async Task<string> NextAccountNumber(XpenseDbContext dbContext, CancellationToken cancellationToken)
    {
        var numbers = await dbContext.Accounts.Select(account => account.AccountNumber).ToListAsync(cancellationToken);

        return numbers.Count == 0
            ? FirstAccountNumber.ToString()
            : (numbers.Max(long.Parse) + 1).ToString();
    }
}
