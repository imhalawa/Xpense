using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Entities;
using Xpense.Services.Exceptions;

namespace Xpense.Services.Features.Transactions.UseCases;

public class GetTransactionByIdUseCase(ITransactionRepository repository)
{
    public async Task<Transaction> Execute(int id, CancellationToken cancellationToken = default)
    {
        var transaction = await repository.GetByIdWithDetails(id);
        return transaction ?? throw new TransactionNotFoundException(id);
    }
}
