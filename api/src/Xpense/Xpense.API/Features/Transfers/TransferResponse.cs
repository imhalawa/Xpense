using System;
using System.Collections.Generic;
using System.Linq;
using Xpense.Domain.Entities;

namespace Xpense.API.Features.Transfers;

public sealed record TransferResponse(
    int Id,
    TransferMoneyResponse Amount,
    int SourceAccountId,
    int DestinationAccountId,
    string Reason,
    string OccurredAt,
    IReadOnlyList<TransferLegResponse> Legs)
{
    public static TransferResponse Of(Transfer transfer) => new(
        transfer.Id,
        new TransferMoneyResponse(transfer.Amount, transfer.Currency.ToString()),
        transfer.SourceAccountId,
        transfer.DestinationAccountId,
        transfer.Reason,
        new DateTimeOffset(transfer.CreatedOn).ToUniversalTime().ToString("O"),
        transfer.Legs
            .OrderBy(leg => leg.Direction)
            .Select(leg => new TransferLegResponse(
                leg.Id,
                leg.AccountId,
                leg.Direction.ToString().ToLowerInvariant(),
                new TransferMoneyResponse(leg.Amount, leg.Currency.ToString())))
            .ToArray());
}

public sealed record TransferMoneyResponse(long Cents, string Currency);

public sealed record TransferLegResponse(
    int Id,
    int AccountId,
    string Direction,
    TransferMoneyResponse Amount);
