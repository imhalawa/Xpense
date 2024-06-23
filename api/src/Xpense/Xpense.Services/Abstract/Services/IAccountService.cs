using Xpense.Services.Models.Account;

namespace Xpense.Services.Abstract.Services
{
    public interface IAccountService
    {
        Account GetAccount(string accountId);
    }
}
