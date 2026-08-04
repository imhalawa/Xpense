using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Abstract.UseCases;
using Xpense.Services.Exceptions;

namespace Xpense.Services.Features.Accounts.Usecases;

public class DeleteAccountUseCase(IAccountRepository repository): ICommandHandler<int>
{
    public async Task Handle(int id)
    {
       var account = await repository.GetById(id);
       if (account == null)
           throw new AccountNotFoundException(id);

       repository.Delete(account);
       var result = await repository.SaveChanges();
       if (result < 1)
       {
           throw new AccountUpdateFailedException(id.ToString());
       }
    }
}
