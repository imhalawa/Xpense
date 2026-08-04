using System;
using System.Collections.Generic;
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
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;
using Xpense.Domain.Exceptions;
using Xpense.Domain.ValueObjects;
using Xpense.Domain.Options;

namespace Xpense.API.Features.Transactions;

/// <summary>
/// One endpoint for both directions. The old code had DepositTransactionUseCase and
/// WithdrawTransactionUseCase, which were identical apart from Deposit vs Withdraw and
/// Credit vs Debit -- roughly 60 duplicated lines to express a two-line difference.
/// </summary>
public sealed class CreateTransaction : IEndpoint
{
    public sealed record Request(
        string Type,
        MoneyRequest Amount,
        string AccountNumber,
        int CategoryId,
        OptionRequest Merchant,
        IReadOnlyList<OptionRequest> Tags,
        DateTimeOffset? OccurredAt);

    public sealed record MoneyRequest(long Cents, string Currency);

    public sealed record OptionRequest(int? Id, string Label, bool Create);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Type)
                .Must(type => TryParseKind(type, out _))
                .WithMessage("The type must be either 'income' or 'expense'.");

            RuleFor(request => request.Amount)
                .NotNull().WithMessage("The amount is required.");

            When(request => request.Amount is not null, () =>
            {
                RuleFor(request => request.Amount.Cents)
                    .GreaterThan(0).WithMessage("The amount in cents must be positive.");

                RuleFor(request => request.Amount.Currency)
                    .Must(currency => CurrencyParser.TryParse(currency, out _))
                    .WithMessage("The currency must be a supported currency name.");
            });

            RuleFor(request => request.CategoryId)
                .GreaterThan(0).WithMessage("The categoryId must reference an existing category.");

            RuleFor(request => request.Merchant)
                .NotNull().WithMessage("The merchant is required.");
        }
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/transactions", Handle).WithName(nameof(CreateTransaction)).Validated();

    private static async Task<Created<TransactionResponse>> Handle(
        Request request,
        XpenseDbContext db,
        OptionResolver<Merchant> merchants,
        OptionResolver<Tag> tags,
        HttpContext http,
        CancellationToken ct)
    {
        // The validator already rejected anything else.
        TryParseKind(request.Type, out var isIncome);
        CurrencyParser.TryParse(request.Amount.Currency, out var currency);

        var account = await ResolveAccount(db, request.AccountNumber, ct);

        var category = await db.Categories
            .Include(item => item.Priority)
            .FirstOrDefaultAsync(item => item.Id == request.CategoryId, ct)
            ?? throw new CategoryNotFoundException(request.CategoryId);

        // Deposit/Withdraw reject an amount whose currency differs from the account's, so a
        // USD transaction against a EUR account fails here rather than moving the wrong number.
        var amount = Money.OfCents(request.Amount.Cents, currency);
        if (isIncome)
            account.Deposit(amount);
        else
            account.Withdraw(amount);

        var merchant = await merchants.Resolve(ToOption(request.Merchant), ct)
                       ?? throw new MerchantNotFoundException(request.Merchant.Label);

        var transaction = new Transaction
        {
            Amount = request.Amount.Cents,
            Currency = currency,
            Category = category,
            Account = account,
            CreatedOn = request.OccurredAt?.UtcDateTime ?? DateTime.UtcNow,
            Tags = await ResolveTags(tags, request.Tags, ct),
            Merchant = merchant,
            TransactionType = isIncome ? TransactionType.Credit : TransactionType.Debit
        };

        db.Transactions.Add(transaction);

        if (await db.SaveChangesAsync(ct) < 1)
        {
            throw isIncome
                ? new DepositCreationFailedException(amount.ToDecimal(), request.AccountNumber)
                : new WithdrawCreationFailedException(amount.ToDecimal(), request.AccountNumber);
        }

        return TypedResults.Created(
            http.ResourceUri($"/api/v1/transactions/{transaction.Id}"),
            TransactionResponse.Of(transaction));
    }

    private static async Task<Account> ResolveAccount(XpenseDbContext db, string accountNumber, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
            return await db.Accounts.FirstOrDefaultAsync(account => account.IsDefaultAccount, ct)
                   ?? throw new DefaultAccountNotFoundException();

        return await db.Accounts.FirstOrDefaultAsync(account => account.AccountNumber == accountNumber, ct)
               ?? throw new AccountNotFoundException(accountNumber);
    }

    private static async Task<List<Tag>> ResolveTags(
        OptionResolver<Tag> resolver,
        IReadOnlyList<OptionRequest> requested,
        CancellationToken ct)
    {
        if (requested is null)
            return null;

        List<Tag> resolved = [];
        foreach (var option in requested)
        {
            if (await resolver.Resolve(ToTagOption(option), ct) is { } tag)
                resolved.Add(tag);
        }

        return resolved;
    }

    private static MerchantOption ToOption(OptionRequest option) =>
        new() { Id = option.Id, Label = option.Label, Create = option.Create };

    private static TagOption ToTagOption(OptionRequest option) =>
        new() { Id = option.Id, Label = option.Label, Create = option.Create };

    private static bool TryParseKind(string type, out bool isIncome)
    {
        isIncome = string.Equals(type, "income", StringComparison.OrdinalIgnoreCase);
        return isIncome || string.Equals(type, "expense", StringComparison.OrdinalIgnoreCase);
    }

}
