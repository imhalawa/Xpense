using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Abstract.UseCases;
using Xpense.Services.Entities;

namespace Xpense.Services.Features.Transactions.UseCases
{
    public class GetAllTransactionsUseCase(ITransactionRepository repository) : IQueryHandler<IEnumerable<Transaction>>
    {
        public async Task<IEnumerable<Transaction>> Execute()
        {
            return await repository.GetAllTransactions();
        }
    }
}
