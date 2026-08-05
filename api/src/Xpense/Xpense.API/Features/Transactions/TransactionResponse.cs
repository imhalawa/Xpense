using System.Collections.Generic;
using System.Linq;
using Xpense.API.Contracts;
using Xpense.Domain.Entities;

namespace Xpense.API.Features.Transactions;

/// <summary>
/// One shape for all three kinds. A null account side means the money crossed the system boundary,
/// and the merchant names who was on that side; a transfer has both sides and neither a merchant
/// nor a category.
/// </summary>
public sealed record TransactionResponse(
    int Id,
    string Kind,
    MoneyResponse Amount,
    string? SourceAccountNumber,
    string? DestinationAccountNumber,
    int? CategoryId,
    TransactionOptionResponse? Merchant,
    IReadOnlyList<TransactionOptionResponse> Tags,
    string? Reason,
    string OccurredAt,
    string CreatedAt,
    string? UpdatedAt)
{
    public static TransactionResponse Of(Transaction transaction) => new(
        transaction.Id,
        transaction.Kind.ToString().ToLowerInvariant(),
        MoneyResponse.Of(transaction.Amount),
        transaction.SourceAccount?.AccountNumber,
        transaction.DestinationAccount?.AccountNumber,
        transaction.CategoryId,
        transaction.Merchant is null
            ? null
            : new TransactionOptionResponse(transaction.Merchant.Id, transaction.Merchant.Label),
        transaction.Tags?
            .Select(tag => new TransactionOptionResponse(tag.Id, tag.Label))
            .ToArray() ?? [],
        transaction.Reason,
        Timestamps.Iso(transaction.OccurredAt),
        Timestamps.Iso(transaction.CreatedAt),
        Timestamps.Iso(transaction.UpdatedAt));
}

public sealed record TransactionOptionResponse(int Id, string Label);

public sealed record TransactionPageResponse(
    IReadOnlyList<TransactionResponse> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);
