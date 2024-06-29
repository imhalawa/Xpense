using Xpense.Services.Interfaces.Persistence;
using Xpense.Services.Interfaces.UseCases;
using Xpense.Services.Models.Response.Account;

namespace Xpense.Services.UseCases.Account;

public class GetAccountByNumberUseCase(IAccountRepository repository) : IQueryParamHandler<string, GetAccountResponse>
{
    public async Task<GetAccountResponse> Execute(string accountNumber, CancellationToken cancellationToken = default)
    {
        var account = await repository.GetAccountByNumber(accountNumber);
        return GetAccountResponse.Of(account);
    }
}