using System;
using System.Collections.Generic;
using System.Linq;
using Xpense.Services.Enums;
using Xpense.Services.Exceptions;
using Xpense.Services.Features.Transactions.Commands;
using Xpense.Services.ValueObjects;
using ServiceMerchant = Xpense.Services.Models.Merchant;
using ServiceTag = Xpense.Services.Models.Tag;

namespace Xpense.API.Models.Requests;

public sealed record CreateTransactionRequest
{
    public string Type { get; init; } = null!;
    public TransactionMoney Amount { get; init; } = null!;

    public string? AccountNumber { get; init; }
    public int CategoryId { get; init; }
    public TransactionMerchant Merchant { get; init; } = null!;

    public IReadOnlyList<TransactionTag>? Tags { get; init; }

    public DateTimeOffset? OccurredAt { get; init; }

    /// <summary>
    /// The wire vocabulary is income/expense; the domain records Credit/Debit.
    /// </summary>
    public bool TryGetTransactionType(out TransactionType transactionType)
    {
        switch (Type?.ToLowerInvariant())
        {
            case "income":
                transactionType = TransactionType.Credit;
                return true;
            case "expense":
                transactionType = TransactionType.Debit;
                return true;
            default:
                transactionType = default;
                return false;
        }
    }

    /// <summary>
    /// Null when the currency is absent or is not a supported currency <em>name</em>. Returning
    /// null rather than taking an out parameter keeps the caller on one path.
    /// <para>
    /// The name check is not redundant: Enum.TryParse also accepts the underlying number, so
    /// "0" would parse as EUR and let a caller submit a value that is not a currency at all.
    /// </para>
    /// </summary>
    public Currency? GetCurrency() =>
        Amount is not null
        && !string.IsNullOrWhiteSpace(Amount.Currency)
        && Enum.GetNames<Currency>().Any(name =>
            string.Equals(name, Amount.Currency, StringComparison.OrdinalIgnoreCase))
        && Enum.TryParse<Currency>(Amount.Currency, ignoreCase: true, out var currency)
            ? currency
            : null;

    /// <summary>
    /// Guards the mapping path as well as the validation path, so a caller that skipped
    /// validation gets a stated failure rather than a silently mistyped transaction.
    /// </summary>
    public TransactionType RequireTransactionType() =>
        TryGetTransactionType(out var transactionType)
            ? transactionType
            : throw new UnsupportedTransactionTypeException(Type);

    public DepositTransactionCommand ToDepositCommand() => new(
        ToMoney(),
        AccountNumber,
        CategoryId,
        ToMerchant(),
        ToTags(),
        OccurredAt?.ToUnixTimeSeconds());

    public WithdrawTransactionCommand ToWithdrawCommand() => new(
        ToMoney(),
        AccountNumber,
        CategoryId,
        ToMerchant(),
        ToTags(),
        OccurredAt?.ToUnixTimeSeconds());

    private Money ToMoney() => Money.OfCents(
        Amount.Cents,
        GetCurrency() ?? throw new UnsupportedCurrencyException(Amount?.Currency));

    private ServiceMerchant ToMerchant() => new()
    {
        Id = Merchant.Id,
        Label = Merchant.Label,
        Create = Merchant.Create
    };

    private ServiceTag[]? ToTags() => Tags?.Select(tag => new ServiceTag
    {
        Id = tag.Id,
        Label = tag.Label,
        Create = tag.Create
    }).ToArray();
}

// Parts of a request, not requests in their own right - no endpoint accepts one on its own,
// so they are not named *Request.
public sealed record TransactionMoney(long Cents, string Currency);

public sealed record TransactionMerchant(int? Id, string Label, bool Create);

public sealed record TransactionTag(int? Id, string Label, bool Create);
