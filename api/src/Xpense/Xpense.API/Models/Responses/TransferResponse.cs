#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xpense.Services.Entities;
using Xpense.Services.ValueObjects;

namespace Xpense.API.Models.Responses;

public sealed record TransferResponse(
    int Id,
    TransferMoneyResponse Amount,
    int SourceAccountId,
    int DestinationAccountId,
    string? Reason,
    string OccurredAt,
    IReadOnlyList<TransferLegResponse> Legs)
{
    public static TransferResponse Of(Transfer transfer) => new(
        transfer.Id,
        TransferMoneyResponse.Of(Money.OfCents(transfer.Amount, transfer.Currency)),
        transfer.SourceAccountId,
        transfer.DestinationAccountId,
        transfer.Reason,
        new DateTimeOffset(transfer.CreatedOn).ToUniversalTime().ToString("O"),
        transfer.Legs
            .OrderBy(leg => leg.Direction)
            .Select(TransferLegResponse.Of)
            .ToArray());
}

public sealed record TransferMoneyResponse(long Cents, string Currency)
{
    public static TransferMoneyResponse Of(Money money) =>
        new(money.Cents, money.Currency.ToString());
}

public sealed record TransferLegResponse(
    int Id,
    int AccountId,
    string Direction,
    TransferMoneyResponse Amount)
{
    public static TransferLegResponse Of(TransferLeg leg) => new(
        leg.Id,
        leg.AccountId,
        leg.Direction.ToString().ToLowerInvariant(),
        TransferMoneyResponse.Of(Money.OfCents(leg.Amount, leg.Currency)));
}
