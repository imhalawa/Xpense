using Xpense.Services.Entities;

namespace Xpense.Services.Abstract.Services
{
    public interface IAccountService
    {
        Account GetAccount(string accountId);
    }
}
