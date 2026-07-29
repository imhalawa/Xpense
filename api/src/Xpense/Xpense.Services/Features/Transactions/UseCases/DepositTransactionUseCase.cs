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
        if (!string.IsNullOrWhiteSpace(command.AccountNumber) && !await accountRepository.Exists(command.AccountNumber))
            throw new AccountNotFoundException(command.AccountNumber);

        var category = await categoryRepository.GetWithById(command.CategoryId, s => s.Priority) ?? throw new CategoryNotFoundException(command.CategoryId);

        var account = string.IsNullOrWhiteSpace(command.AccountNumber)
                           ? await accountRepository.GetDefaultAccount()
                           : await accountRepository.GetAccountByNumber(command.AccountNumber);

        account.Deposit(command.Amount.ToDecimal());

        var merchant = await merchantRepository.GetOrCreateIfMissing(command.Merchant) ?? throw new MerchantNotFoundException(command.Merchant.Label);

        var tags = await ResolveTags(tagRepository, command.Tags);

        var transaction = new Transaction
        {
            Amount = command.Amount.Cents,
            Currency = command.Amount.Currency,
            Category = category,
            Account = account,
            CreatedOn = command.CreatedOn.ToDateTime() ?? DateTime.UtcNow,
            Tags = tags,
            Merchant = merchant,
            TransactionType = TransactionType.Credit
        };

        transactionRepository.Create(transaction);
        var result = await transactionRepository.SaveChanges();

        if (result < 1)
            throw new DepositCreationFailedException(command.Amount.ToDecimal(), command.AccountNumber);

        return transaction;
    }

    /// <summary>
    /// Resolves each requested tag, creating it when missing and dropping any that could not be
    /// resolved. This used to run through IAsyncEnumerable over an in-memory array, which net10
    /// no longer supports the same way -- and which was only ever a sequential loop.
    /// </summary>
    internal static async Task<List<Tag>?> ResolveTags(ITagRepository tagRepository, Models.Tag[]? requested)
    {
        if (requested is null)
            return null;

        List<Tag> tags = [];
        foreach (var tag in requested)
        {
            var resolved = await tagRepository.GetOrCreateIfMissing(tag);
            if (resolved != null)
                tags.Add(resolved);
        }

        return tags;
    }
}