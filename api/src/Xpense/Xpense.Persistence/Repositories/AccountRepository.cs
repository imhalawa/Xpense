using Microsoft.EntityFrameworkCore;
using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Entities;
using Xpense.Services.Exceptions;

namespace Xpense.Persistence.Repositories;

public class AccountRepository(XpenseDbContext context) : Repository<Account>(context), IAccountRepository
{
    public string GetNextAccountNumber()
    {
        // Warning: Extremely costly operation!
        // Try to find a solution for such problems
        var lastGeneratedNumber = DbSet.Select(c => long.Parse(c.AccountNumber)).ToList().Max();
        return (lastGeneratedNumber + 1).ToString();
    }

    public bool HasDefaultAccount()
    {
        return DbSet.Any(c => c.IsDefaultAccount);
    }

    public void DeleteAccountByNumber(string accountNumber)
    {
        try
        {
            var account = DbSet.First(account => account.AccountNumber == accountNumber);
            account.MarkAsDeleted();
            account.Touch();
            DbSet.Update(account);
        }
        catch (Exception ex)
        {
            throw new AccountNotFoundException(accountNumber, ex);
        }
    }

    public async Task<Account> GetAccountByNumber(string accountNumber)
    {
        try
        {
            return await DbSet.FirstAsync(a => a.AccountNumber == accountNumber);
        }
        catch (Exception ex)
        {
            throw new AccountNotFoundException(accountNumber, ex);
        }
    }
}