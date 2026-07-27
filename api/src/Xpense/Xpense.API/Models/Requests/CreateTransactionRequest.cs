using System;
using System.Collections.Generic;
using System.Linq;
using Xpense.Services.Enums;
using Xpense.Services.Features.Transactions.Commands;
using Xpense.Services.ValueObjects;
using ServiceMerchant = Xpense.Services.Models.Merchant;
using ServiceTag = Xpense.Services.Models.Tag;

namespace Xpense.API.Models.Requests;

public enum TransactionKind
{
    Income,
    Expense
}

public sealed class CreateTransactionRequest
{
    public string Type { get; init; } = null!;
    public TransactionMoneyRequest Amount { get; init; } = null!;

    public string? AccountNumber { get; init; }
    public int CategoryId { get; init; }
    public TransactionMerchantRequest Merchant { get; init; } = null!;

    public IReadOnlyList<TransactionTagRequest>? Tags { get; init; }

    public DateTimeOffset? OccurredAt { get; init; }

    public bool TryGetKind(out TransactionKind kind)
    {
        if (string.Equals(Type, "income", StringComparison.OrdinalIgnoreCase))
        {
            kind = TransactionKind.Income;
            return true;
        }

        if (string.Equals(Type, "expense", StringComparison.OrdinalIgnoreCase))
        {
            kind = TransactionKind.Expense;
            return true;
        }

        kind = default;
        return false;
    }

    public bool TryGetCurrency(out Currency currency)
    {
        currency = default;
        return Amount is not null
            && !string.IsNullOrWhiteSpace(Amount.Currency)
            && Enum.GetNames<Currency>().Any(name => string.Equals(name, Amount.Currency, StringComparison.OrdinalIgnoreCase))
            && Enum.TryParse(Amount.Currency, true, out currency);
    }

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

    private Money ToMoney()
    {
        if (!TryGetCurrency(out var currency))
            throw new InvalidOperationException("The currency must be a supported currency name.");

        return Money.OfCents(Amount.Cents, currency);
    }

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

public sealed record TransactionMoneyRequest(long Cents, string Currency);

public sealed record TransactionMerchantRequest(int? Id, string Label, bool Create);

public sealed record TransactionTagRequest(int? Id, string Label, bool Create);
