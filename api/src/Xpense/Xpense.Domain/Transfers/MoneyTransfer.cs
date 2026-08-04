using Xpense.Domain.Entities;
using Xpense.Domain.Enums;
using Xpense.Domain.Exceptions;
using Xpense.Domain.ValueObjects;

namespace Xpense.Domain.Transfers;

/// <summary>
/// Moving money between accounts is domain logic, not endpoint plumbing, so it stays here
/// rather than being inlined into the CreateTransfer slice. It enforces the invariants and
/// produces the debit/credit legs; the slice owns loading the accounts and the atomic boundary.
/// </summary>
public static class MoneyTransfer
{
    public static Transfer Between(
        Account source,
        Account destination,
        Money amount,
        string reason,
        DateTime occurredAt)
    {
        if (amount.Cents <= 0)
            throw new InvalidTransferException("Transfer amount must be positive.");

        if (source.Id == destination.Id)
            throw new InvalidTransferException("Source and destination accounts must be different.");

        // Xpense holds multiple currencies but does not convert between them. Both accounts and
        // the amount have to agree; otherwise this moves the wrong quantity of money, which is
        // exactly what it used to do when Balance was a currency-less decimal.
        if (source.Currency != destination.Currency)
            throw new InvalidTransferException(
                "Cannot transfer between accounts in different currencies: "
                + $"{source.AccountNumber} is {source.Currency}, {destination.AccountNumber} is {destination.Currency}.");

        if (amount.Currency != source.Currency)
            throw new CurrencyMismatchException(source.AccountNumber, source.Currency, amount.Currency);

        if (source.Balance < amount)
            throw new InsufficientFundsForTransferException(source.Id, source.Balance, amount);

        source.Withdraw(amount);
        destination.Deposit(amount);

        var transfer = new Transfer
        {
            Amount = amount.Cents,
            Currency = amount.Currency,
            SourceAccount = source,
            DestinationAccount = destination,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            CreatedOn = occurredAt,
            Legs = []
        };

        transfer.Legs.Add(Leg(transfer, source, TransferLegDirection.Debit, amount));
        transfer.Legs.Add(Leg(transfer, destination, TransferLegDirection.Credit, amount));

        return transfer;
    }

    private static TransferLeg Leg(Transfer transfer, Account account, TransferLegDirection direction, Money amount) =>
        new()
        {
            Transfer = transfer,
            Account = account,
            Direction = direction,
            Amount = amount.Cents,
            Currency = amount.Currency,
            CreatedOn = transfer.CreatedOn
        };
}
