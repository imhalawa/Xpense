using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Abstract.UseCases;
using Xpense.Services.Entities;

namespace Xpense.Services.Features.Accounts.Usecases;

public class GetAccountByNumberUseCase(IAccountRepository repository) : IQueryParamHandler<string, Account>
{
    public async Task<Account> Execute(string accountNumber, CancellationToken cancellationToken = default)
    {
        var account = await repository.GetAccountByNumber(accountNumber);
        return account;
    }
}