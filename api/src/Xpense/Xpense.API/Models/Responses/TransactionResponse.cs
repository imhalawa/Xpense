using System;
using System.Collections.Generic;
using System.Linq;
using Xpense.Services.Abstract.Entities;
using Xpense.Services.Entities;
using Xpense.Services.Enums;
using Xpense.Services.Models;
using Xpense.Services.ValueObjects;

namespace Xpense.API.Models.Responses;

public sealed record TransactionResponse(
    int Id,
    string Type,
    TransactionMoneyResponse Amount,
    int? AccountId,
    int CategoryId,
    TransactionOptionResponse Merchant,
    IReadOnlyList<TransactionOptionResponse> Tags,
    string OccurredAt,
    string? UpdatedAt)
{
    public static TransactionResponse Of(Transaction transaction) => new(
        transaction.Id,
        WireType(transaction.TransactionType),
        TransactionMoneyResponse.Of(Money.OfCents(transaction.Amount, transaction.Currency)),
        transaction.AccountId,
        transaction.CategoryId,
        TransactionOptionResponse.Of(transaction.Merchant),
        transaction.Tags?.Select(TransactionOptionResponse.Of).ToArray() ?? [],
        Timestamp(transaction.CreatedOn),
        transaction.LastUpdated is null ? null : Timestamp(transaction.LastUpdated.Value));

    /// <summary>
    /// The wire vocabulary is income/expense; the domain records Credit/Debit. Translating in one
    /// place keeps every endpoint from re-deriving the mapping.
    /// </summary>
    private static string WireType(TransactionType type) => type switch
    {
        TransactionType.Credit => "income",
        TransactionType.Debit => "expense",
        _ => type.ToString().ToLowerInvariant()
    };

    private static string Timestamp(DateTime value) =>
        new DateTimeOffset(value).ToUniversalTime().ToString("O");
}

public sealed record TransactionMoneyResponse(long Cents, string Currency)
{
    public static TransactionMoneyResponse Of(Money money) =>
        new(money.Cents, money.Currency.ToString());
}

public sealed record TransactionOptionResponse(int Id, string Label)
{
    /// <summary>
    /// Merchants and tags both surface as an id and a label, so one factory serves both.
    /// </summary>
    public static TransactionOptionResponse Of(IOptionEntity option) =>
        new(option.Id, option.Label);
}

public sealed record TransactionPageResponse(
    IReadOnlyList<TransactionResponse> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages)
{
    public static TransactionPageResponse Of(PaginatedResult<Transaction> result) => new(
        result.Data.Select(TransactionResponse.Of).ToArray(),
        result.Page,
        result.Size,
        result.TotalItems,
        result.Pages);
}
