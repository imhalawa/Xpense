using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Abstract.UseCases;
using Xpense.Services.Exceptions;

namespace Xpense.Services.Features.Accounts.Usecases;

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