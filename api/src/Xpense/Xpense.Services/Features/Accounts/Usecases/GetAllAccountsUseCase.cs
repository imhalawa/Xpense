using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Abstract.UseCases;
using Xpense.Services.Features.Accounts.Responses;

namespace Xpense.Services.Features.Accounts.Usecases;

public class GetAllAccountsUseCase(IAccountRepository repository): IQueryHandler<IEnumerable<GetAccountResponse>>
{
    public async Task<IEnumerable<GetAccountResponse>> Execute()
    {
        var result = await repository.GetAll();
        return result.Select(GetAccountResponse.Of);
    }
}