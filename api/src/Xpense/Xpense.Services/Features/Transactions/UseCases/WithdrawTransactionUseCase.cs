using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Abstract.UseCases;
using Xpense.Services.Entities;
using Xpense.Services.Enums;
using Xpense.Services.Exceptions;
using Xpense.Services.Features.Transactions.Commands;

namespace Xpense.Services.Features.Transactions.UseCases;

public class WithdrawTransactionUseCase(
    IAccountRepository accountRepository,
    ICategoryRepository categoryRepository,
    ITagRepository tagRepository,
    ITransactionRepository transactionRepository
) : ICommandResultHandler<WithdrawTransactionCommand, Transaction>
{
    public async Task<Transaction> Handle(WithdrawTransactionCommand command)
    {
        // Could be enhanced by de-duplicating the queries of Account & Category
        if (!string.IsNullOrWhiteSpace(command.FromAccount) && !await accountRepository.Exists(command.FromAccount))
            throw new AccountNotFoundException(command.FromAccount);

        if (!await categoryRepository.Exists(command.Category))
            throw new CategoryNotFoundException(command.Category);

        var tags = command.Tags == null ? null : await tagRepository.GetAll(command.Tags);
        var account = string.IsNullOrWhiteSpace(command.FromAccount)
            ? await accountRepository.GetDefaultAccount()
            : await accountRepository.GetAccountByNumber(command.FromAccount);
        var category = await categoryRepository.GetById(command.Category);

        // DomainEvents would be a good option here
        account.Withdraw(command.Amount);

        var transaction = new Transaction
        {
            Amount = command.Amount,
            Reason = command.Reason,
            Category = category,
            ToAccount = account,
            Tags = tags,
            TransactionType = TransactionType.Withdraw
        };

        transactionRepository.Create(transaction);
        var result = await transactionRepository.SaveChanges();

        if (result < 1)
            throw new WithdrawCreationFailedException(command.Amount, command.FromAccount);

        return transaction;
    }
}