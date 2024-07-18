using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Abstract.UseCases;
using Xpense.Services.Entities;
using Xpense.Services.Enums;
using Xpense.Services.Exceptions;
using Xpense.Services.Features.Transactions.Commands;
using Xpense.Services.Helpers;

namespace Xpense.Services.Features.Transactions.UseCases;

public class DepositTransactionUseCase(
    IAccountRepository accountRepository,
    ICategoryRepository categoryRepository,
    ITagRepository tagRepository,
    ITransactionRepository transactionRepository,
    IMerchantRepository merchantRepository
) : ICommandResultHandler<DepositTransactionCommand, Transaction>
{
    public async Task<Transaction> Handle(DepositTransactionCommand command)
    {
        if (!string.IsNullOrWhiteSpace(command.ToAccountNumber) && !await accountRepository.Exists(command.ToAccountNumber))
            throw new AccountNotFoundException(command.ToAccountNumber);

        var category = await categoryRepository.GetWithById(command.CategoryId, s => s.Priority) ?? throw new CategoryNotFoundException(command.CategoryId);

        var account = string.IsNullOrWhiteSpace(command.ToAccountNumber)
                           ? await accountRepository.GetDefaultAccount()
                           : await accountRepository.GetAccountByNumber(command.ToAccountNumber);

        account.Deposit(command.Amount.ToSingle());

        var merchant = await merchantRepository.GetOrCreateIfMissing(command.Merchant) ?? throw new MerchantNotFoundException(command.Merchant.Label);

        var tags = command.Tags != null
            ? await command.Tags.ToAsyncEnumerable()
                .SelectAwait(async t => await tagRepository.GetOrCreateIfMissing(t))
                .Where(t => t != null).ToListAsync()
            : null;

        var transaction = new Transaction
        {
            Amount = command.Amount.Cents,
            Currency = command.Amount.Currency,
            Category = category,
            Account = account,
            CreatedOn = command.CreatedOn.ToDateTime() ?? DateTime.Now,
            Tags = tags,
            Merchant = merchant,
            TransactionType = TransactionType.Credit
        };

        transactionRepository.Create(transaction);
        var result = await transactionRepository.SaveChanges();

        if (result < 1)
            throw new DepositCreationFailedException(command.Amount.ToSingle(), command.ToAccountNumber);

        return transaction;
    }
}