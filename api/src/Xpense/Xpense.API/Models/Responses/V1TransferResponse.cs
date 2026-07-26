#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xpense.Services.Entities;

namespace Xpense.API.Models.Responses;

public sealed record V1TransferResponse(
    int Id,
    V1TransferMoneyResponse Amount,
    int SourceAccountId,
    int DestinationAccountId,
    string? Reason,
    string OccurredAt,
    IReadOnlyList<V1TransferLegResponse> Legs)
{
    public static V1TransferResponse From(Transfer transfer)
    {
        return new V1TransferResponse(
            transfer.Id,
            new V1TransferMoneyResponse(transfer.Amount, transfer.Currency.ToString()),
            transfer.SourceAccountId,
            transfer.DestinationAccountId,
            transfer.Reason,
            new DateTimeOffset(transfer.CreatedOn).ToUniversalTime().ToString("O"),
            transfer.Legs
                .OrderBy(leg => leg.Direction)
                .Select(leg => new V1TransferLegResponse(
                    leg.Id,
                    leg.AccountId,
                    leg.Direction.ToString().ToLowerInvariant(),
                    new V1TransferMoneyResponse(leg.Amount, leg.Currency.ToString())))
                .ToArray());
    }
}

public sealed record V1TransferMoneyResponse(long Cents, string Currency);

public sealed record V1TransferLegResponse(
    int Id,
    int AccountId,
    string Direction,
    V1TransferMoneyResponse Amount);
