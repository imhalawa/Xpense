using System;
using System.Collections.Generic;
using System.Linq;
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;

namespace Xpense.API.Features.Transactions;

public sealed record TransactionResponse(
    int Id,
    string Type,
    TransactionMoneyResponse Amount,
    int? AccountId,
    int CategoryId,
    TransactionOptionResponse Merchant,
    IReadOnlyList<TransactionOptionResponse> Tags,
    string OccurredAt,
    string UpdatedAt)
{
    public static TransactionResponse Of(Transaction transaction) => new(
        transaction.Id,
        transaction.TransactionType switch
        {
            TransactionType.Credit => "income",
            TransactionType.Debit => "expense",
            _ => transaction.TransactionType.ToString().ToLowerInvariant()
        },
        new TransactionMoneyResponse(transaction.Amount, transaction.Currency.ToString()),
        transaction.AccountId,
        transaction.CategoryId,
        new TransactionOptionResponse(transaction.Merchant.Id, transaction.Merchant.Label),
        transaction.Tags?
            .Select(tag => new TransactionOptionResponse(tag.Id, tag.Label))
            .ToArray() ?? [],
        new DateTimeOffset(transaction.CreatedOn).ToUniversalTime().ToString("O"),
        transaction.LastUpdated is null
            ? null
            : new DateTimeOffset(transaction.LastUpdated.Value).ToUniversalTime().ToString("O"));
}

public sealed record TransactionMoneyResponse(long Cents, string Currency);

public sealed record TransactionOptionResponse(int Id, string Label);

public sealed record TransactionPageResponse(
    IReadOnlyList<TransactionResponse> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);
