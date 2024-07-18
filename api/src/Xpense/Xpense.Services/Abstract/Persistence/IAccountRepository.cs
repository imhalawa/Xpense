using Xpense.Services.Entities;

namespace Xpense.Services.Abstract.Persistence;

public interface IAccountRepository : IRepository<Account>
{
    string GetNextAccountNumber();
    bool HasDefaultAccount();
    void DeleteAccountByNumber(string accountNumber);
    Task<Account> GetAccountByNumber(string accountNumber);
    Task<Account> GetDefaultAccount();
    Task<bool> Exists(string accountNumber);
}
