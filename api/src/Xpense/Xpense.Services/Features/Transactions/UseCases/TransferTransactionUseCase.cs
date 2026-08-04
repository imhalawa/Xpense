using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Abstract.UseCases;
using Xpense.Services.Entities;
using Xpense.Services.Enums;
using Xpense.Services.Exceptions;
using Xpense.Services.Features.Transactions.Commands;

namespace Xpense.Services.Features.Transactions.UseCases;

public sealed class TransferTransactionUseCase(
    IAccountRepository accountRepository,
    ITransferRepository transferRepository)
    : ICommandResultHandler<TransferTransactionCommand, Transfer>
{
    public Task<Transfer> Handle(TransferTransactionCommand command)
    {
        if (command.Amount.Cents <= 0)
            throw new InvalidTransferException("Transfer amount must be positive.");

        if (command.SourceAccountId == command.DestinationAccountId)
            throw new InvalidTransferException("Source and destination accounts must be different.");

        return transferRepository.ExecuteAtomic(async () =>
        {
            var source = await accountRepository.GetById(command.SourceAccountId)
                ?? throw new AccountNotFoundException(command.SourceAccountId);
            var destination = await accountRepository.GetById(command.DestinationAccountId)
                ?? throw new AccountNotFoundException(command.DestinationAccountId);
            var amount = command.Amount.ToDecimal();

            if (source.Balance < amount)
                throw new InsufficientFundsForTransferException(source.Id, source.Balance, amount);

            source.Withdraw(amount);
            destination.Deposit(amount);

            var transfer = new Transfer
            {
                Amount = command.Amount.Cents,
                Currency = command.Amount.Currency,
                SourceAccount = source,
                DestinationAccount = destination,
                Reason = string.IsNullOrWhiteSpace(command.Reason) ? null : command.Reason.Trim(),
                CreatedOn = command.CreatedOn.HasValue
                    ? DateTimeOffset.FromUnixTimeSeconds(command.CreatedOn.Value).UtcDateTime
                    : DateTime.UtcNow,
                Legs = new List<TransferLeg>()
            };

            transfer.Legs.Add(CreateLeg(transfer, source, TransferLegDirection.Debit, command));
            transfer.Legs.Add(CreateLeg(transfer, destination, TransferLegDirection.Credit, command));
            return transfer;
        });
    }

    private static TransferLeg CreateLeg(
        Transfer transfer,
        Account account,
        TransferLegDirection direction,
        TransferTransactionCommand command)
    {
        return new TransferLeg
        {
            Transfer = transfer,
            Account = account,
            Direction = direction,
            Amount = command.Amount.Cents,
            Currency = command.Amount.Currency,
            CreatedOn = transfer.CreatedOn
        };
    }
}
