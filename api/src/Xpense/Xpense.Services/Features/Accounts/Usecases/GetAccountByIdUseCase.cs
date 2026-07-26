using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Abstract.UseCases;
using Xpense.Services.Entities;
using Xpense.Services.Exceptions;

namespace Xpense.Services.Features.Accounts.Usecases;

public class GetAccountByIdUseCase(IAccountRepository repository) : IQueryParamHandler<int, Account>
{
    public async Task<Account> Execute(int id, CancellationToken cancellationToken = default)
    {
        var account = await repository.GetById(id);
        return account ?? throw new AccountNotFoundException(id);
    }
}
