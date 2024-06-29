using Xpense.Services.Interfaces.Persistence;
using Xpense.Services.Interfaces.UseCases;
using Xpense.Services.Models.Response.Account;

namespace Xpense.Services.UseCases.Account;

public class GetAllAccountsUseCase(IAccountRepository repository): IQueryHandler<IEnumerable<GetAccountResponse>>
{
    public async Task<IEnumerable<GetAccountResponse>> Execute()
    {
        var result = await repository.GetAll();
        return result.Select(GetAccountResponse.Of);
    }
}