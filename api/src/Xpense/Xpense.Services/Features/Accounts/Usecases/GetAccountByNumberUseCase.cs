using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Abstract.UseCases;
using Xpense.Services.Features.Accounts.Responses;

namespace Xpense.Services.Features.Accounts.Usecases;

public class GetAccountByNumberUseCase(IAccountRepository repository) : IQueryParamHandler<string, GetAccountResponse>
{
    public async Task<GetAccountResponse> Execute(string accountNumber, CancellationToken cancellationToken = default)
    {
        var account = await repository.GetAccountByNumber(accountNumber);
        return GetAccountResponse.Of(account);
    }
}