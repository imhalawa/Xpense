using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Abstract.UseCases;
using Xpense.Services.Entities;

namespace Xpense.Services.Features.Accounts.Usecases;

public class GetAllAccountsUseCase(IAccountRepository repository): IQueryHandler<IEnumerable<Account>>
{
    public async Task<IEnumerable<Account>> Execute()
    {
        var result = await repository.GetAll();
        return result;
    }
}