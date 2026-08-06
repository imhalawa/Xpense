using System;
using System.Collections.Generic;
using System.Data;
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
using Xpense.Domain.Options;
using Xpense.Domain.ValueObjects;

namespace Xpense.API.Features.Transactions;

/// <summary>
/// One endpoint for all three kinds. Which sides the caller names decides the kind, so there is no
/// type field to contradict them: naming only a destination is income, only a source is expense,
/// both is a transfer. This replaced a separate /transfers resource over the same entity.
/// <para>
/// The caller must name at least one account. The default-account fallback that the old
/// income/expense endpoint had is gone: it relied on a type field to know which side the default
/// account stood in for, and that field no longer exists.
/// </para>
/// </summary>
public sealed class CreateTransaction : IEndpoint
{
    public sealed record Request(
        MoneyRequest Amount,
        string? SourceAccountNumber,
        string? DestinationAccountNumber,
        int? CategoryId,
        OptionRequest? Merchant,
        IReadOnlyList<OptionRequest>? Tags,
        string? Reason,
        DateTimeOffset? OccurredAt);

    public sealed record MoneyRequest(long MinorUnits, string Currency);

    public sealed record OptionRequest(int? Id, string Label, bool Create);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Amount)
                .NotNull().WithMessage("The amount is required.");

            When(request => request.Amount is not null, () =>
            {
                RuleFor(request => request.Amount.MinorUnits)
                    .GreaterThan(0).WithMessage("The amount in minor units must be positive.");

                RuleFor(request => request.Amount.Currency)
                    .Must(currency => CurrencyParser.TryParse(currency, out _))
                    .WithMessage("The currency must be a supported currency name.");
            });

            RuleFor(request => request)
                .Must(request => Names(request.SourceAccountNumber) || Names(request.DestinationAccountNumber))
                .WithMessage(
                    "Either a source account or a destination account is required.")
                .WithName(nameof(Request.SourceAccountNumber));

            // A transaction with one account inside Xpense has a counterparty outside it, which the
            // merchant names, and a spending class. A transfer has neither: no shop, no spending.
            When(IsOneSided, () =>
            {
                RuleFor(request => request.CategoryId)
                    .NotNull().WithMessage("A category is required unless both accounts are named.")
                    .GreaterThan(0).WithMessage("The category must be a valid selection.");

                RuleFor(request => request.Merchant)
                    .NotNull().WithMessage("The merchant is required unless both accounts are named.");
            });

            When(IsTransfer, () =>
            {
                RuleFor(request => request.CategoryId)
                    .Null().WithMessage("A transfer between two accounts cannot have a category.");

                RuleFor(request => request.Merchant)
                    .Null().WithMessage("A transfer between two accounts cannot have a merchant.");

                // Also guarded in the domain. That is not redundancy to remove: this produces a
                // good 400 for a bad request, and the domain guard makes the move impossible
                // through any future caller.
                RuleFor(request => request.DestinationAccountNumber)
                    .Must((request, destination) =>
                        !string.Equals(request.SourceAccountNumber, destination, StringComparison.Ordinal))
                    .WithMessage("Source and destination accounts must be different.");
            });
        }

        private static bool IsOneSided(Request request) =>
            Names(request.SourceAccountNumber) ^ Names(request.DestinationAccountNumber);

        private static bool IsTransfer(Request request) =>
            Names(request.SourceAccountNumber) && Names(request.DestinationAccountNumber);

        private static bool Names(string? accountNumber) => !string.IsNullOrWhiteSpace(accountNumber);
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
        CurrencyParser.TryParse(request.Amount.Currency, out var currency);

        var amount = Money.OfMinorUnits(request.Amount.MinorUnits, currency);
        var occurredAt = request.OccurredAt?.UtcDateTime ?? DateTime.UtcNow;

        // Serializable because every kind reads a balance and writes it back. The old transfer
        // endpoint isolated this and the old income/expense endpoint did not; one path means one
        // answer, and the safer one is the correct one.
        await using var scope = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        try
        {
            // Loaded inside the transaction, not before it. Postgres only detects conflicts on
            // reads made within the transaction, so a balance read outside it leaves the
            // insufficient-funds guard unprotected: two concurrent transfers from one account would
            // both see the old balance, both pass the check, and both commit.
            var source = await FindAccount(db, request.SourceAccountNumber, ct);
            var destination = await FindAccount(db, request.DestinationAccountNumber, ct);
            var resolvedTags = await ResolveTags(tags, request.Tags, ct);

            var transaction = source is not null && destination is not null
                ? Domain.Entities.Transaction.Transfer(source, destination, amount, request.Reason, resolvedTags, occurredAt)
                : await OneSided(db, merchants, request, source, destination, amount, resolvedTags, occurredAt, ct);

            db.Transactions.Add(transaction);

            if (await db.SaveChangesAsync(ct) < 1)
                throw Failure(request, amount, transaction.Kind);

            await scope.CommitAsync(ct);

            return TypedResults.Created(
                http.ResourceUri($"/api/v1/transactions/{transaction.Id}"),
                TransactionResponse.Of(transaction));
        }
        catch
        {
            await scope.RollbackAsync(ct);
            throw;
        }
    }

    private static async Task<Transaction> OneSided(
        XpenseDbContext db,
        OptionResolver<Merchant> merchants,
        Request request,
        Account? source,
        Account? destination,
        Money amount,
        List<Tag>? tags,
        DateTime occurredAt,
        CancellationToken ct)
    {
        var category = await db.Categories
            .Include(item => item.Priority)
            .FirstOrDefaultAsync(item => item.Id == request.CategoryId, ct)
            ?? throw new CategoryNotFoundException(request.CategoryId!.Value);

        var merchant = await merchants.Resolve(ToMerchantOption(request.Merchant!), ct)
                       ?? throw new MerchantNotFoundException(request.Merchant!.Label);

        // Deposit and Withdraw reject an amount whose currency differs from the account's, so a USD
        // transaction against a EUR account fails here rather than moving the wrong number.
        return destination is not null
            ? Domain.Entities.Transaction.Income(destination, amount, category, merchant, tags, occurredAt)
            : Domain.Entities.Transaction.Expense(source!, amount, category, merchant, tags, occurredAt);
    }

    /// <summary>
    /// Defensive: adding an entity and saving returns at least one write or throws. Reported against
    /// the side the money was heading for, so a transfer names its destination rather than being
    /// mislabelled as a plain deposit.
    /// </summary>
    private static Exception Failure(Request request, Money amount, TransactionKind kind) =>
        kind == TransactionKind.Expense
            ? new WithdrawCreationFailedException(amount.ToDecimal(), request.SourceAccountNumber!)
            : new DepositCreationFailedException(amount.ToDecimal(), request.DestinationAccountNumber!);

    private static async Task<Account?> FindAccount(XpenseDbContext db, string? accountNumber, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
            return null;

        return await db.Accounts.FirstOrDefaultAsync(account => account.AccountNumber == accountNumber, ct)
               ?? throw new AccountNotFoundException(accountNumber);
    }

    private static async Task<List<Tag>?> ResolveTags(
        OptionResolver<Tag> resolver,
        IReadOnlyList<OptionRequest>? requested,
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

    private static MerchantOption ToMerchantOption(OptionRequest option) =>
        new() { Id = option.Id, Label = option.Label, Create = option.Create };

    private static TagOption ToTagOption(OptionRequest option) =>
        new() { Id = option.Id, Label = option.Label, Create = option.Create };
}
