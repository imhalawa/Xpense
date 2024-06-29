using Xpense.Services.Interfaces.Persistence;
using Xpense.Services.Interfaces.UseCases;
using Xpense.Services.Models.Response.Account;

namespace Xpense.Services.UseCases.Account;

public class UpdateAccountUseCase (IAccountRepository repository) : ICommandResultHandler<UpdateAccountCommand,GetAccountResponse>
{
    public async Task<GetAccountResponse> Handle(UpdateAccountCommand command)
    {
        var account = await repository.GetAccountByNumber(command.Number);
        account.Name = command.Name;
        account.IsDefaultAccount = command.IsDefault;
        repository.Update(account);
        repository.SaveChanges();
        return GetAccountResponse.Of(account);
    }
}