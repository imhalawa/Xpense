using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Entities;

namespace Xpense.Services.Features.Transactions.UseCases;

public class GetTransactionByIdUseCase(ITransactionRepository repository)
{
    public Task<Transaction?> Execute(int id, CancellationToken cancellationToken = default)
    {
        return repository.GetByIdWithDetails(id);
    }
}
