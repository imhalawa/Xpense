using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Abstract.UseCases;
using Xpense.Services.Entities;
using Xpense.Services.Enums;
using Xpense.Services.Exceptions;
using Xpense.Services.Features.Transactions.Commands;
using Xpense.Services.Helpers;

namespace Xpense.Services.Features.Transactions.UseCases;

public class WithdrawTransactionUseCase(
    IAccountRepository accountRepository,
    ICategoryRepository categoryRepository,
    ITagRepository tagRepository,
    ITransactionRepository transactionRepository,
        IMerchantRepository merchantRepository
) : ICommandResultHandler<WithdrawTransactionCommand, Transaction>
{
    public async Task<Transaction> Handle(WithdrawTransactionCommand command)
    {
        if (!string.IsNullOrWhiteSpace(command.AccountNumber) && !await accountRepository.Exists(command.AccountNumber))
            throw new AccountNotFoundException(command.AccountNumber);

        var category = await categoryRepository.GetWithById(command.CategoryId, s => s.Priority) ?? throw new CategoryNotFoundException(command.CategoryId);

        var account = string.IsNullOrWhiteSpace(command.AccountNumber)
            ? await accountRepository.GetDefaultAccount()
            : await accountRepository.GetAccountByNumber(command.AccountNumber);

        account.Withdraw(command.Amount.ToSingle());

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
            TransactionType = TransactionType.Debit
        };

        transactionRepository.Create(transaction);
        var result = await transactionRepository.SaveChanges();

        if (result < 1)
            throw new DepositCreationFailedException(command.Amount.ToSingle(), command.AccountNumber);

        return transaction;
    }
}