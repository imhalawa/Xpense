using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Abstract.UseCases;
using Xpense.Services.Features.Accounts.Commands;
using Xpense.Services.Features.Accounts.Responses;

namespace Xpense.Services.Features.Accounts.Usecases;

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