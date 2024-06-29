using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Abstract.UseCases;
using Xpense.Services.Exceptions;
using Xpense.Services.Features.Accounts.Commands;
using Xpense.Services.Features.Accounts.Responses;

namespace Xpense.Services.Features.Accounts.Usecases;

public class CreateAccountUseCase(IAccountRepository repository) : ICommandResultHandler<CreateAccountCommand, CreateAccountResponse>
{
    public async Task<CreateAccountResponse> Handle(CreateAccountCommand request)
    {
        var account = new Entities.Account()
        {
            Name = request.Name,
            Balance = request.Balance,
            AccountNumber = repository.GetNextAccountNumber(),
            IsDefaultAccount = !repository.HasDefaultAccount(),
        };

        repository.Create(account);
        var result = await repository.SaveChanges();

        if (result < 1)
        {
            throw new AccountCreationFailedException(request.Name);
        }

        return CreateAccountResponse.Of(account);
    }
}