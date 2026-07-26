using System;
using System.Collections.Generic;
using System.Linq;
using Xpense.Services.Entities;
using Xpense.Services.Enums;
using Xpense.Services.Models;

namespace Xpense.API.Models.Responses;

public sealed record V1TransactionResponse(
    int Id,
    string Type,
    V1TransactionMoneyResponse Amount,
    int? AccountId,
    int CategoryId,
    V1TransactionOptionResponse Merchant,
    IReadOnlyList<V1TransactionOptionResponse> Tags,
    string OccurredAt,
    string? UpdatedAt)
{
    public static V1TransactionResponse From(Transaction transaction)
    {
        return new V1TransactionResponse(
            transaction.Id,
            transaction.TransactionType switch
            {
                TransactionType.Credit => "income",
                TransactionType.Debit => "expense",
                _ => transaction.TransactionType.ToString().ToLowerInvariant()
            },
            new V1TransactionMoneyResponse(transaction.Amount, transaction.Currency.ToString()),
            transaction.AccountId,
            transaction.CategoryId,
            new V1TransactionOptionResponse(transaction.Merchant.Id, transaction.Merchant.Label),
            transaction.Tags?.Select(tag => new V1TransactionOptionResponse(tag.Id, tag.Label)).ToArray() ?? [],
            new DateTimeOffset(transaction.CreatedOn).ToUniversalTime().ToString("O"),
            transaction.LastUpdated is null ? null : new DateTimeOffset(transaction.LastUpdated.Value).ToUniversalTime().ToString("O"));
    }
}

public sealed record V1TransactionMoneyResponse(long Cents, string Currency);

public sealed record V1TransactionOptionResponse(int Id, string Label);

public sealed record V1TransactionPageResponse(
    IReadOnlyList<V1TransactionResponse> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages)
{
    public static V1TransactionPageResponse From(PaginatedResult<Transaction> result)
    {
        return new V1TransactionPageResponse(
            result.Data.Select(V1TransactionResponse.From).ToArray(),
            result.Page,
            result.Size,
            result.TotalItems,
            result.Pages);
    }
}
