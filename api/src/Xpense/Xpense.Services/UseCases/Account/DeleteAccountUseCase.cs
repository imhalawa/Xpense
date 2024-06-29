using Xpense.Services.Exceptions;
using Xpense.Services.Interfaces.Persistence;
using Xpense.Services.Interfaces.UseCases;

namespace Xpense.Services.UseCases.Account;

public class DeleteAccountUseCase(IAccountRepository repository): ICommandHandler<string>
{
    public async Task Handle(string accountNumber)
    {
       repository.DeleteAccountByNumber(accountNumber);
       var result = await repository.SaveChanges();
       if (result < 1)
       {
           throw new AccountUpdateFailedException(accountNumber);
       }
    }
}