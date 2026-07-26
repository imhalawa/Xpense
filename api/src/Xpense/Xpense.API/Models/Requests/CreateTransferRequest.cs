#nullable enable
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Xpense.Services.Enums;
using Xpense.Services.Features.Transactions.Commands;
using Xpense.Services.ValueObjects;

namespace Xpense.API.Models.Requests;

public sealed class CreateTransferRequest
{
    [Range(1, int.MaxValue)]
    public int SourceAccountId { get; init; }

    [Range(1, int.MaxValue)]
    public int DestinationAccountId { get; init; }

    [Required]
    public TransferMoneyRequest Amount { get; init; } = null!;

    [MaxLength(500)]
    public string? Reason { get; init; }

    public DateTimeOffset? OccurredAt { get; init; }

    public bool TryGetCurrency(out Currency currency)
    {
        currency = default;
        return Amount is not null
            && !string.IsNullOrWhiteSpace(Amount.Currency)
            && Enum.GetNames<Currency>().Any(name => string.Equals(name, Amount.Currency, StringComparison.OrdinalIgnoreCase))
            && Enum.TryParse(Amount.Currency, true, out currency);
    }

    public TransferTransactionCommand ToCommand()
    {
        if (!TryGetCurrency(out var currency))
            throw new InvalidOperationException("The currency must be a supported currency name.");

        return new TransferTransactionCommand(
            Money.OfCents(Amount.Cents, currency),
            SourceAccountId,
            DestinationAccountId,
            Reason,
            OccurredAt?.ToUnixTimeSeconds());
    }
}

public sealed record TransferMoneyRequest(long Cents, string Currency);
