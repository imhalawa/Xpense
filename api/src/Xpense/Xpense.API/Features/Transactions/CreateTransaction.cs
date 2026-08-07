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
using Xpense.Domain.Events;
using Xpense.Domain.Exceptions;
using Xpense.Domain.Options;
using Xpense.Domain.ValueObjects;

namespace Xpense.API.Features.Transactions;

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
        XpenseDbContext dbContext,
        OptionResolver<Merchant> merchants,
        OptionResolver<Tag> tags,
        IEventBus events,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        CurrencyParser.TryParse(request.Amount.Currency, out var currency);

        var amount = Money.OfMinorUnits(request.Amount.MinorUnits, currency);
        var occurredAt = request.OccurredAt?.UtcDateTime ?? DateTime.UtcNow;

        await using var scope = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            var source = await FindAccount(dbContext, request.SourceAccountNumber, cancellationToken);
            var destination = await FindAccount(dbContext, request.DestinationAccountNumber, cancellationToken);
            var resolvedTags = await ResolveTags(tags, request.Tags, cancellationToken);

            var transaction = source is not null && destination is not null
                ? Domain.Entities.Transaction.Transfer(source, destination, amount, request.Reason, resolvedTags, occurredAt)
                : await OneSided(dbContext, merchants, request, source, destination, amount, resolvedTags, occurredAt, cancellationToken);

            dbContext.Transactions.Add(transaction);

            if (await dbContext.SaveChangesAsync(cancellationToken) < 1)
                throw Failure(request, amount, transaction.Kind);

            await events.Emit(Event.Of(Recorded(transaction), occurredAt), cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            await scope.CommitAsync(cancellationToken);

            return TypedResults.Created(
                httpContext.ResourceUri($"/api/v1/transactions/{transaction.Id}"),
                TransactionResponse.Of(transaction));
        }
        catch
        {
            await scope.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static TransactionRecorded Recorded(Transaction transaction) =>
        new(
            transaction.Id,
            transaction.Kind,
            transaction.AmountMinorUnits,
            transaction.Currency,
            transaction.OccurredAt,
            transaction.CategoryId,
            transaction.MerchantId,
            transaction.SourceAccount?.AccountNumber,
            transaction.SourceAccount?.BalanceMinorUnits,
            transaction.DestinationAccount?.AccountNumber,
            transaction.DestinationAccount?.BalanceMinorUnits);

    private static async Task<Transaction> OneSided(
        XpenseDbContext dbContext,
        OptionResolver<Merchant> merchants,
        Request request,
        Account? source,
        Account? destination,
        Money amount,
        List<Tag>? tags,
        DateTime occurredAt,
        CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories
            .Include(item => item.Priority)
            .FirstOrDefaultAsync(item => item.Id == request.CategoryId, cancellationToken)
            ?? throw new CategoryNotFoundException(request.CategoryId!.Value);

        var merchant = await merchants.Resolve(ToMerchantOption(request.Merchant!), cancellationToken)
                       ?? throw new MerchantNotFoundException(request.Merchant!.Label);

        return destination is not null
            ? Domain.Entities.Transaction.Income(destination, amount, category, merchant, tags, occurredAt)
            : Domain.Entities.Transaction.Expense(source!, amount, category, merchant, tags, occurredAt);
    }

    private static Exception Failure(Request request, Money amount, TransactionKind kind) =>
        kind == TransactionKind.Expense
            ? new WithdrawCreationFailedException(amount.ToDecimal(), request.SourceAccountNumber!)
            : new DepositCreationFailedException(amount.ToDecimal(), request.DestinationAccountNumber!);

    private static async Task<Account?> FindAccount(XpenseDbContext dbContext, string? accountNumber, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
            return null;

        return await dbContext.Accounts.FirstOrDefaultAsync(account => account.AccountNumber == accountNumber, cancellationToken)
               ?? throw new AccountNotFoundException(accountNumber);
    }

    private static async Task<List<Tag>?> ResolveTags(
        OptionResolver<Tag> resolver,
        IReadOnlyList<OptionRequest>? requested,
        CancellationToken cancellationToken)
    {
        if (requested is null)
            return null;

        List<Tag> resolved = [];
        foreach (var option in requested)
        {
            if (await resolver.Resolve(ToTagOption(option), cancellationToken) is { } tag)
                resolved.Add(tag);
        }

        return resolved;
    }

    private static MerchantOption ToMerchantOption(OptionRequest option) =>
        new() { Id = option.Id, Label = option.Label, Create = option.Create };

    private static TagOption ToTagOption(OptionRequest option) =>
        new() { Id = option.Id, Label = option.Label, Create = option.Create };
}
