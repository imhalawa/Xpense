using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Abstract.UseCases;
using Xpense.Services.Entities;
using Xpense.Services.Exceptions;
using Xpense.Services.Features.Accounts.Commands;

namespace Xpense.Services.Features.Accounts.Usecases;

public class UpdateAccountUseCase(IAccountRepository repository)
    : ICommandResultHandler<UpdateAccountCommand, Account>
{
    public async Task<Account> Handle(UpdateAccountCommand command)
    {
        var account = await repository.GetAccountByNumber(command.Number);
        account.Name = command.Name;
        account.IsDefaultAccount = command.IsDefault;
        repository.Update(account);
        var result = await repository.SaveChanges();
        if (result < 1)
            throw new AccountUpdateFailedException(command.Number);
        return account;
    }
}