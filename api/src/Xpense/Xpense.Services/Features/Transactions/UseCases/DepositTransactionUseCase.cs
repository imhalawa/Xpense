using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Abstract.UseCases;
using Xpense.Services.Entities;
using Xpense.Services.Enums;
using Xpense.Services.Exceptions;
using Xpense.Services.Features.Transactions.Commands;

namespace Xpense.Services.Features.Transactions.UseCases;

public class DepositTransactionUseCase(
    IAccountRepository accountRepository,
    ICategoryRepository categoryRepository,
    ITagRepository tagRepository,
    ITransactionRepository transactionRepository
) : ICommandResultHandler<DepositTransactionCommand, Transaction>
{
    public async Task<Transaction> Handle(DepositTransactionCommand command)
    {
        // Could be enhanced by de-duplicating the queries of Account & Category
        if (!string.IsNullOrWhiteSpace(command.ToAccount) && !await accountRepository.Exists(command.ToAccount))
            throw new AccountNotFoundException(command.ToAccount);

        if (!await categoryRepository.Exists(command.Category))
            throw new CategoryNotFoundException(command.Category);

        var tags = command.Tags == null ? null : await tagRepository.GetAll(command.Tags);
        var account = string.IsNullOrWhiteSpace(command.ToAccount)
            ? await accountRepository.GetDefaultAccount()
            : await accountRepository.GetAccountByNumber(command.ToAccount);
        var category = await categoryRepository.GetById(command.Category);

        // DomainEvents would be a good option here
        account.Deposit(command.Amount);

        var transaction = new Transaction
        {
            Amount = command.Amount,
            Reason = command.Reason,
            Category = category,
            ToAccount = account,
            Tags = tags,
            TransactionType = TransactionType.Deposit
        };

        transactionRepository.Create(transaction);
        var result = await transactionRepository.SaveChanges();

        if (result < 1)
            throw new DepositCreationFailedException(command.Amount, command.ToAccount);

        return transaction;
    }
}